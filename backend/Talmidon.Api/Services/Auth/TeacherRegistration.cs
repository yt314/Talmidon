using Microsoft.AspNetCore.Identity;
using Talmidon.Api.Contracts;
using Talmidon.Domain.Entities;
using Talmidon.Infrastructure.Auth;
using Talmidon.Infrastructure.Data;
using Talmidon.Infrastructure.Identity;

namespace Talmidon.Api.Services.Auth;

/// <summary>תוצאת הרשמה: התקבלה, נדחתה בגלל הסיסמה (השגיאות בטוחות להצגה), או נכשלה בשרת.</summary>
public sealed record RegistrationResult(IReadOnlyList<string>? PasswordErrors = null, bool ServerError = false)
{
    public static readonly RegistrationResult Accepted = new();
}

/// <summary>
/// הרשמת מורה עצמאית: משתמש + תפקיד Teacher + ישות מורה (הדייר) בעסקה אחת, ואז מייל אימות.
/// </summary>
public class TeacherRegistration(
    TalmidonDbContext db,
    UserManager<ApplicationUser> userManager,
    AuthEmails emails,
    ILogger<TeacherRegistration> logger)
{
    /// <summary>
    /// מייל שכבר רשום מחזיר "התקבלה" בלי לעשות דבר — אותה תגובה כמו להרשמה חדשה, כדי לא
    /// לחשוף אילו כתובות קיימות במערכת (user enumeration).
    /// </summary>
    public async Task<RegistrationResult> RegisterAsync(RegisterRequest request, string requestBaseUrl)
    {
        if (await userManager.FindByEmailAsync(request.Email) is not null)
            return RegistrationResult.Accepted;

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
            return new RegistrationResult(PasswordErrors: createResult.Errors.Select(e => e.Description).ToList());
        }

        var roleResult = await userManager.AddToRoleAsync(user, Roles.Teacher);
        if (!roleResult.Succeeded)
        {
            await transaction.RollbackAsync();
            logger.LogError("Failed to assign Teacher role: {Errors}",
                string.Join("; ", roleResult.Errors.Select(e => e.Description)));
            return new RegistrationResult(ServerError: true);
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

        await emails.SendConfirmationAsync(user, request.FullName, requestBaseUrl);
        return RegistrationResult.Accepted;
    }
}
