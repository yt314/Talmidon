using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Talmidon.Api.Contracts;
using Talmidon.Api.Services;
using Talmidon.Api.Services.Auth;
using Talmidon.Infrastructure.Identity;

namespace Talmidon.Api.Controllers;

/// <summary>
/// הרשמה, אימות מייל, התחברות, אסימונים וסיסמאות. הבקר מתרגם בין HTTP לשירותים
/// שב-Services/Auth; תגובות שעלולות לחשוף אם כתובת רשומה — זהות תמיד.
/// </summary>
[ApiController]
[EnableRateLimiting("auth")]
[Route("api/auth")]
public class AuthController(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    TeacherRegistration registration,
    RefreshTokens refreshTokens,
    AuthEmails emails,
    IConfiguration configuration) : ControllerBase
{
    // תגובה גנרית זהה — מונעת חשיפה אם המייל כבר קיים (user enumeration)
    private const string GenericRegisterMessage =
        "אם הכתובת אינה רשומה עדיין, נשלח אליה מייל לאימות. נא לבדוק את תיבת הדואר.";

    private const string GenericForgotPasswordMessage =
        "אם הכתובת רשומה במערכת, נשלח אליה קישור לאיפוס סיסמה. נא לבדוק את תיבת הדואר.";

    private const string InvalidLinkMessage = "קישור לא תקין.";

    private string RequestBaseUrl => $"{Request.Scheme}://{Request.Host}";

    /// <summary>הרשמת מורה עצמאית: יוצר משתמש + תפקיד Teacher + ישות מורה (דייר) + שולח מייל אימות.</summary>
    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest request)
    {
        var result = await registration.RegisterAsync(request, RequestBaseUrl);
        if (result.PasswordErrors is not null) return BadRequest(new { errors = result.PasswordErrors });
        if (result.ServerError)
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "ההרשמה נכשלה. נסה שוב מאוחר יותר." });
        return Ok(new { message = GenericRegisterMessage });
    }

    /// <summary>שליחה חוזרת של מייל האימות (לחשבון שטרם אומת). תגובה גנרית תמיד.</summary>
    [AllowAnonymous]
    [HttpPost("resend-confirmation")]
    public async Task<IActionResult> ResendConfirmation(ResendConfirmationRequest request)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is not null && !await userManager.IsEmailConfirmedAsync(user))
            await emails.SendConfirmationAsync(user, user.DisplayName ?? user.Email!, RequestBaseUrl);

        return Ok(new { message = GenericRegisterMessage });
    }

    /// <summary>שליחת קישור לאיפוס סיסמה למי ששכח/ה אותה. תגובה גנרית תמיד (מניעת חשיפת קיום מייל).</summary>
    [AllowAnonymous]
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is not null)
            await emails.SendPasswordResetAsync(user, user.DisplayName ?? user.Email!);

        return Ok(new { message = GenericForgotPasswordMessage });
    }

    /// <summary>
    /// אימות כתובת המייל, מהקישור במייל — ולכן שתי התוצאות מפנות חזרה לאפליקציה.
    ///
    /// Opened by clicking the link in the confirmation email, so both outcomes
    /// land in the app. A failure used to answer with a JSON body, which a
    /// browser shows as a bare line of text on a white page with no way back —
    /// at the very first step of using the product. The reasons are ordinary:
    /// a link older than its lifetime, or one a mail client wrapped and cut.
    /// The login screen takes it from here and offers to send a new one.
    /// </summary>
    [AllowAnonymous]
    [HttpGet("confirm-email")]
    public async Task<IActionResult> ConfirmEmail([FromQuery] string? userId, [FromQuery] string? token)
    {
        // מניעת דליפת הטוקן דרך Referer בעת ההפניה
        Response.Headers.Append("Referrer-Policy", "no-referrer");

        var clientUrl = configuration["App:ClientUrl"] ?? "/";
        var failed = Redirect($"{clientUrl}/login?confirmed=0");

        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(token))
            return failed;

        var user = await userManager.FindByIdAsync(userId);
        var decoded = AuthEmails.Decode(token);
        if (user is null || decoded is null) return failed;

        var result = await userManager.ConfirmEmailAsync(user, decoded);
        return result.Succeeded ? Redirect($"{clientUrl}/login?confirmed=1") : failed;
    }

    /// <summary>קביעת סיסמה מקישור הזמנה (הורה/תלמיד שהמורה יצרה) או מאיפוס. מאמת גם את המייל.</summary>
    [AllowAnonymous]
    [HttpPost("set-password")]
    public async Task<IActionResult> SetPassword(SetPasswordRequest request)
    {
        var user = await userManager.FindByIdAsync(request.UserId);
        var decoded = AuthEmails.Decode(request.Token);
        if (user is null || decoded is null)
            return BadRequest(new { message = InvalidLinkMessage });

        var result = await userManager.ResetPasswordAsync(user, decoded, request.Password);
        if (!result.Succeeded)
            return BadRequest(new { errors = result.Errors.Select(e => e.Description) });

        // קביעת סיסמה דרך קישור ההזמנה מוכיחה בעלות על המייל
        if (!user.EmailConfirmed)
        {
            user.EmailConfirmed = true;
            await userManager.UpdateAsync(user);
        }

        return Ok(new { message = "הסיסמה הוגדרה בהצלחה. אפשר להתחבר." });
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null)
            return Unauthorized(new { message = "אימייל או סיסמה שגויים." });

        // CheckPasswordSignInAsync מטפל בנעילה (lockoutOnFailure) ובאימות מייל (RequireConfirmedEmail)
        var result = await signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);

        if (result.IsLockedOut)
            return Unauthorized(new { message = "החשבון נעול. אם מדובר בטעות, יש לפנות לתמיכה." });
        if (result.IsNotAllowed)
            return Unauthorized(new { message = "יש לאמת את כתובת המייל לפני התחברות." });
        if (!result.Succeeded)
            return Unauthorized(new { message = "אימייל או סיסמה שגויים." });

        return Ok(await refreshTokens.IssueAsync(user));
    }

    [AllowAnonymous]
    [HttpPost("refresh")]
    public async Task<ActionResult<AuthResponse>> Refresh(RefreshRequest request)
    {
        var result = await refreshTokens.RotateAsync(request.RefreshToken);
        return result.Error is null ? Ok(result.Value) : this.ToHttp(result.Error);
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(RefreshRequest request)
    {
        // רק הבעלים של האסימון רשאי לשלול אותו
        await refreshTokens.RevokeAsync(request.RefreshToken, User.FindFirstValue(ClaimTypes.NameIdentifier));
        return NoContent();
    }

    /// <summary>שינוי סיסמה למשתמש מחובר. מבטל את כל אסימוני הרענון הפעילים (התחברות מחדש נדרשת בכל מכשיר).</summary>
    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("No user id in the current context.");
        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
            return Unauthorized();

        var result = await userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
            return BadRequest(new { errors = result.Errors.Select(e => e.Description) });

        await refreshTokens.RevokeAllAsync(user.Id);
        return Ok(new { message = "הסיסמה עודכנה בהצלחה. יש להתחבר מחדש." });
    }
}
