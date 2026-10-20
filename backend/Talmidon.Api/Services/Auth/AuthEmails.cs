using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Talmidon.Infrastructure.Diagnostics;
using Talmidon.Infrastructure.Email;
using Talmidon.Infrastructure.Identity;

namespace Talmidon.Api.Services.Auth;

/// <summary>מיילי החשבון: אימות כתובת ואיפוס סיסמה. כישלון שליחה נרשם ביומן התקלות ואינו מפיל את הבקשה.</summary>
public class AuthEmails(
    UserManager<ApplicationUser> userManager,
    IEmailSender emailSender,
    IConfiguration configuration,
    IErrorLog errorLog)
{
    /// <param name="requestBaseUrl">
    /// כתובת הבקשה הנוכחית — משמשת רק כשאין App:ApiBaseUrl בקונפיג. הבסיס האמין הוא הקונפיג,
    /// לא Request.Host (מניעת host-header injection).
    /// </param>
    public async Task SendConfirmationAsync(ApplicationUser user, string fullName, string requestBaseUrl)
    {
        var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
        var apiBase = configuration["App:ApiBaseUrl"];
        var baseUrl = string.IsNullOrWhiteSpace(apiBase) ? requestBaseUrl : apiBase.TrimEnd('/');
        var confirmUrl = $"{baseUrl}/api/auth/confirm-email?userId={user.Id}&token={Encode(token)}";

        var html = EmailLayout.Render(new EmailMessage(
            "ברוכה הבאה לתלמידון",
            Greeting: $"שלום {fullName},",
            Intro: "תודה שנרשמת. נשאר רק לאשר את כתובת המייל:",
            ActionLabel: "אישור כתובת המייל",
            ActionUrl: confirmUrl,
            Note: "אם לא נרשמת, אפשר להתעלם מההודעה."));

        // מייל אימות שלא יצא פירושו מורה שנרשמה ואינה יכולה להיכנס, בלי שאיש יודע.
        await SendAsync(user.Email!, "אימות חשבון תלמידון", html, "auth/confirmation");
    }

    /// <summary>
    /// שולח קישור לעמוד /set-password בלקוח (אותו עמוד שמשמש גם להזמנות הורה/תלמיד) —
    /// קביעת סיסמה חדשה מאמתת מחדש את הבעלות על המייל, ולכן מאשרת אותו גם אם לא היה מאושר.
    /// </summary>
    public async Task SendPasswordResetAsync(ApplicationUser user, string fullName)
    {
        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        var clientUrl = (configuration["App:ClientUrl"] ?? "http://localhost:4200").TrimEnd('/');
        var resetUrl = $"{clientUrl}/set-password?userId={user.Id}&token={Encode(token)}";

        var html = EmailLayout.Render(new EmailMessage(
            "איפוס סיסמה",
            Greeting: $"שלום {fullName},",
            Intro: "קיבלנו בקשה לאיפוס הסיסמה שלך.",
            ActionLabel: "קביעת סיסמה חדשה",
            ActionUrl: resetUrl,
            Note: "אם לא ביקשת לאפס סיסמה, אפשר להתעלם מההודעה — החשבון שלך בטוח."));

        // ואיפוס סיסמה שלא יצא פירושו משתמשת נעולה מחוץ לחשבון שלה.
        await SendAsync(user.Email!, "איפוס סיסמה — תלמידון", html, "auth/password-reset");
    }

    /// <summary>הטוקן של Identity כפי שהוא עובר בקישור — Base64Url של ה-UTF-8 שלו.</summary>
    public static string Encode(string token) => WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

    /// <summary>ההפך מ-<see cref="Encode"/>; null כשהקישור נחתך או שובש בדרך.</summary>
    public static string? Decode(string token)
    {
        try { return Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(token)); }
        catch { return null; }
    }

    private async Task SendAsync(string to, string subject, string html, string errorPath)
    {
        try { await emailSender.SendAsync(to, subject, html); }
        catch (Exception ex) { await errorLog.RecordAsync(ex, new ErrorOrigin { Source = "email", Path = errorPath }); }
    }
}
