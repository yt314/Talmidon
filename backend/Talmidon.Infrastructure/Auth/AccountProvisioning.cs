using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Talmidon.Infrastructure.Email;
using Talmidon.Infrastructure.Identity;

namespace Talmidon.Infrastructure.Auth;

/// <summary>
/// מספק יצירת חשבונות מוזמנים (הורה/תלמיד): המורה יוצרת את החשבון,
/// והמשתמש מקבל מייל הזמנה לקביעת סיסמה משלו (אסימון איפוס סיסמה).
/// </summary>
public interface IAccountProvisioning
{
    /// <summary>יוצר משתמש מוזמן (לא מאומת, סיסמה אקראית) ומשייך תפקיד. ללא שליחת מייל — ראה <see cref="SendInvitationEmailAsync"/>.</summary>
    Task<(ApplicationUser? User, string[] Errors)> CreateInvitedUserAsync(string email, string role, string displayName);

    /// <summary>שולח מייל הזמנה עם קישור לקביעת סיסמה. נקרא לאחר commit.</summary>
    Task SendInvitationEmailAsync(ApplicationUser user, string displayName);
}

public class AccountProvisioning(
    UserManager<ApplicationUser> userManager,
    IEmailSender emailSender,
    IConfiguration configuration,
    ILogger<AccountProvisioning> logger) : IAccountProvisioning
{
    public async Task<(ApplicationUser? User, string[] Errors)> CreateInvitedUserAsync(
        string email, string role, string displayName)
    {
        if (await userManager.FindByEmailAsync(email) is not null)
            return (null, ["כתובת המייל כבר קיימת במערכת."]);

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            DisplayName = displayName
        };

        var createResult = await userManager.CreateAsync(user, GenerateRandomPassword());
        if (!createResult.Succeeded)
            return (null, createResult.Errors.Select(e => e.Description).ToArray());

        var roleResult = await userManager.AddToRoleAsync(user, role);
        if (!roleResult.Succeeded)
            return (null, roleResult.Errors.Select(e => e.Description).ToArray());

        return (user, []);
    }

    public async Task SendInvitationEmailAsync(ApplicationUser user, string displayName)
    {
        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        var encoded = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
        var clientUrl = (configuration["App:ClientUrl"] ?? string.Empty).TrimEnd('/');
        var link = $"{clientUrl}/set-password?userId={user.Id}&token={encoded}";

        var html = EmailLayout.Render(new EmailMessage(
            "הוזמנת לתלמידון",
            Greeting: $"שלום {displayName},",
            Intro: "נוצר עבורך חשבון בתלמידון. נשאר רק לקבוע סיסמה ולהתחיל:",
            ActionLabel: "קביעת סיסמה",
            ActionUrl: link,
            Note: "הקישור תקף לזמן מוגבל."));

        try
        {
            await emailSender.SendAsync(user.Email!, "הזמנה לתלמידון — קביעת סיסמה", html);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send invitation email.");
        }
    }

    /// <summary>סיסמה אקראית חזקה (לא נחשפת — המשתמש קובע משלו). מקיימת את מדיניות הסיסמאות.</summary>
    private static string GenerateRandomPassword()
        => "Aa1!" + Convert.ToBase64String(RandomNumberGenerator.GetBytes(24));
}
