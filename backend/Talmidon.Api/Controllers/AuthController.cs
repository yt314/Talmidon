using System.Net;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Talmidon.Api.Contracts;
using Talmidon.Domain.Entities;
using Talmidon.Infrastructure.Auth;
using Talmidon.Infrastructure.Data;
using Talmidon.Infrastructure.Email;
using Talmidon.Infrastructure.Identity;

namespace Talmidon.Api.Controllers;

[ApiController]
[EnableRateLimiting("auth")]
[Route("api/auth")]
public class AuthController(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    TalmidonDbContext db,
    ITokenService tokenService,
    IEmailSender emailSender,
    IConfiguration configuration,
    ILogger<AuthController> logger) : ControllerBase
{
    // תגובה גנרית זהה — מונעת חשיפה אם המייל כבר קיים (user enumeration)
    private const string GenericRegisterMessage =
        "אם הכתובת אינה רשומה עדיין, נשלח אליה מייל לאימות. נא לבדוק את תיבת הדואר.";

    /// <summary>הרשמת מורה עצמאית: יוצר משתמש + תפקיד Teacher + ישות מורה (דייר) + שולח מייל אימות.</summary>
    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest request)
    {
        // אם המייל כבר רשום — לא חושפים זאת, מחזירים תגובה גנרית זהה
        if (await userManager.FindByEmailAsync(request.Email) is not null)
            return Ok(new { message = GenericRegisterMessage });

        await using var transaction = await db.Database.BeginTransactionAsync();

        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            DisplayName = request.FullName
        };

        var createResult = await userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
        {
            await transaction.RollbackAsync();
            // שגיאות אלו נוגעות לסיסמה שנשלחה (לא לקיום החשבון) — בטוח להחזירן
            return BadRequest(new { errors = createResult.Errors.Select(e => e.Description) });
        }

        var roleResult = await userManager.AddToRoleAsync(user, Roles.Teacher);
        if (!roleResult.Succeeded)
        {
            await transaction.RollbackAsync();
            logger.LogError("Failed to assign Teacher role: {Errors}",
                string.Join("; ", roleResult.Errors.Select(e => e.Description)));
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { message = "ההרשמה נכשלה. נסה שוב מאוחר יותר." });
        }

        db.Teachers.Add(new Teacher
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            FullName = request.FullName,
            Phone = request.Phone,
            IsPublic = false,
            DefaultPricePerLesson = 0,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
        await transaction.CommitAsync();

        await SendConfirmationEmailAsync(user, request.FullName);
        return Ok(new { message = GenericRegisterMessage });
    }

    /// <summary>שליחה חוזרת של מייל האימות (לחשבון שטרם אומת). תגובה גנרית תמיד.</summary>
    [AllowAnonymous]
    [HttpPost("resend-confirmation")]
    public async Task<IActionResult> ResendConfirmation(ResendConfirmationRequest request)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is not null && !await userManager.IsEmailConfirmedAsync(user))
            await SendConfirmationEmailAsync(user, user.DisplayName ?? user.Email!);

        return Ok(new { message = GenericRegisterMessage });
    }

    /// <summary>אימות כתובת המייל (מהקישור במייל). מפנה חזרה לאפליקציית הלקוח.</summary>
    [AllowAnonymous]
    [HttpGet("confirm-email")]
    public async Task<IActionResult> ConfirmEmail([FromQuery] string userId, [FromQuery] string token)
    {
        // מניעת דליפת הטוקן דרך Referer בעת ההפניה
        Response.Headers.Append("Referrer-Policy", "no-referrer");

        var clientUrl = configuration["App:ClientUrl"] ?? "/";
        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
            return BadRequest(new { message = "קישור אימות לא תקין." });

        string decoded;
        try { decoded = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(token)); }
        catch { return BadRequest(new { message = "קישור אימות לא תקין." }); }

        var result = await userManager.ConfirmEmailAsync(user, decoded);
        return result.Succeeded
            ? Redirect($"{clientUrl}/login?confirmed=1")
            : BadRequest(new { message = "אימות נכשל או שפג תוקף הקישור." });
    }

    /// <summary>קביעת סיסמה מקישור הזמנה (הורה/תלמיד שהמורה יצרה). מאמת גם את המייל.</summary>
    [AllowAnonymous]
    [HttpPost("set-password")]
    public async Task<IActionResult> SetPassword(SetPasswordRequest request)
    {
        var user = await userManager.FindByIdAsync(request.UserId);
        if (user is null)
            return BadRequest(new { message = "קישור לא תקין." });

        string decoded;
        try { decoded = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(request.Token)); }
        catch { return BadRequest(new { message = "קישור לא תקין." }); }

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
            return Unauthorized(new { message = "החשבון נעול זמנית עקב ריבוי ניסיונות כושלים." });
        if (result.IsNotAllowed)
            return Unauthorized(new { message = "יש לאמת את כתובת המייל לפני התחברות." });
        if (!result.Succeeded)
            return Unauthorized(new { message = "אימייל או סיסמה שגויים." });

        var roles = await userManager.GetRolesAsync(user);
        var tenantId = await ResolveTenantIdAsync(user, roles);
        return await IssueTokensAsync(user, roles, tenantId);
    }

    [AllowAnonymous]
    [HttpPost("refresh")]
    public async Task<ActionResult<AuthResponse>> Refresh(RefreshRequest request)
    {
        var hash = tokenService.HashToken(request.RefreshToken);
        var stored = await db.RefreshTokens.FirstOrDefaultAsync(r => r.TokenHash == hash);

        if (stored is null)
            return Unauthorized(new { message = "אסימון רענון לא תקין." });

        // שימוש חוזר באסימון שכבר נשלל/הוחלף = חשד לגניבה → שלילת כל המשפחה
        if (!stored.IsActive)
        {
            await RevokeAllActiveTokensAsync(stored.UserId);
            logger.LogWarning("Refresh token reuse detected for user {UserId}; revoked all tokens.", stored.UserId);
            return Unauthorized(new { message = "אסימון רענון לא תקין או שפג תוקפו." });
        }

        var user = await userManager.FindByIdAsync(stored.UserId);
        // אימות מחדש של מצב החשבון בכל רענון
        if (user is null || await userManager.IsLockedOutAsync(user) || !await userManager.IsEmailConfirmedAsync(user))
        {
            stored.RevokedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
            return Unauthorized(new { message = "החשבון אינו זמין כעת." });
        }

        var roles = await userManager.GetRolesAsync(user);
        var tenantId = await ResolveTenantIdAsync(user, roles);

        var access = tokenService.CreateAccessToken(user, roles, tenantId);
        var (rawRefresh, newToken) = tokenService.CreateRefreshToken(user.Id);

        stored.RevokedAt = DateTimeOffset.UtcNow;
        stored.ReplacedByTokenHash = newToken.TokenHash;
        db.RefreshTokens.Add(newToken);
        await db.SaveChangesAsync();

        return Ok(new AuthResponse(access, tokenService.AccessTokenExpiry, rawRefresh, user.Email!, roles.ToArray()));
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(RefreshRequest request)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var hash = tokenService.HashToken(request.RefreshToken);
        var stored = await db.RefreshTokens.FirstOrDefaultAsync(r => r.TokenHash == hash);

        // רק הבעלים של האסימון רשאי לשלול אותו
        if (stored is { RevokedAt: null } && stored.UserId == currentUserId)
        {
            stored.RevokedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
        }
        return NoContent();
    }

    // ----- עזר -----

    private async Task<ActionResult<AuthResponse>> IssueTokensAsync(
        ApplicationUser user, IList<string> roles, Guid? tenantId)
    {
        var access = tokenService.CreateAccessToken(user, roles, tenantId);
        var (rawRefresh, entity) = tokenService.CreateRefreshToken(user.Id);
        db.RefreshTokens.Add(entity);
        await db.SaveChangesAsync();
        return Ok(new AuthResponse(access, tokenService.AccessTokenExpiry, rawRefresh, user.Email!, roles.ToArray()));
    }

    private async Task RevokeAllActiveTokensAsync(string userId)
    {
        var now = DateTimeOffset.UtcNow;
        var active = await db.RefreshTokens
            .Where(r => r.UserId == userId && r.RevokedAt == null)
            .ToListAsync();
        foreach (var token in active)
            token.RevokedAt = now;
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// מאתר את הדייר של המשתמש לפי תפקידו (Teacher → Parent → Student). שימוש ב-IgnoreQueryFilters
    /// כי בהתחברות אין עדיין הקשר דייר; SingleOrDefault אוכף מיפוי 1:1 בין משתמש לישות דומיין.
    /// </summary>
    private async Task<Guid?> ResolveTenantIdAsync(ApplicationUser user, IList<string> roles)
    {
        if (roles.Contains(Roles.Teacher))
        {
            var teacher = await db.Teachers.IgnoreQueryFilters()
                .SingleOrDefaultAsync(t => t.UserId == user.Id);
            return teacher?.Id;
        }
        if (roles.Contains(Roles.Parent))
        {
            var parent = await db.Parents.IgnoreQueryFilters()
                .SingleOrDefaultAsync(p => p.UserId == user.Id);
            return parent?.TenantId;
        }
        if (roles.Contains(Roles.Student))
        {
            var student = await db.Students.IgnoreQueryFilters()
                .SingleOrDefaultAsync(s => s.UserId == user.Id);
            return student?.TenantId;
        }
        return null;
    }

    private async Task SendConfirmationEmailAsync(ApplicationUser user, string fullName)
    {
        var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
        var encoded = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

        // בסיס כתובת אמין מהקונפיג (לא מ-Request.Host — מניעת host-header injection)
        var apiBase = configuration["App:ApiBaseUrl"];
        var baseUrl = string.IsNullOrWhiteSpace(apiBase)
            ? $"{Request.Scheme}://{Request.Host}"
            : apiBase.TrimEnd('/');
        var confirmUrl = $"{baseUrl}/api/auth/confirm-email?userId={user.Id}&token={encoded}";

        var html =
            $"""
            <div dir="rtl" style="font-family:Arial,sans-serif">
              <h2>ברוכה הבאה לתלמידון 🎓</h2>
              <p>שלום {WebUtility.HtmlEncode(fullName)},</p>
              <p>תודה שנרשמת. לאישור החשבון לחצי על הקישור:</p>
              <p><a href="{confirmUrl}">אישור כתובת המייל</a></p>
              <p style="color:#888;font-size:12px">אם לא נרשמת, ניתן להתעלם מהודעה זו.</p>
            </div>
            """;

        try
        {
            await emailSender.SendAsync(user.Email!, "אימות חשבון תלמידון", html);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send confirmation email.");
        }
    }
}
