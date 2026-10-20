using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Talmidon.Domain.Entities;
using Talmidon.Domain.Enums;
using Talmidon.Infrastructure.Data;
using Talmidon.Infrastructure.Diagnostics;
using Talmidon.Infrastructure.Email;
using Talmidon.Infrastructure.Identity;
using Talmidon.Infrastructure.Multitenancy;

namespace Talmidon.Api.Services;

/// <summary>
/// מודיע למורה (הדייר הנוכחי) על בקשות שמגיעות מהמשפחה: התראה במרכז ההתראות ומייל.
/// </summary>
public class TeacherNotifier(
    TalmidonDbContext db,
    ICurrentTenant currentTenant,
    UserManager<ApplicationUser> userManager,
    IEmailSender emailSender,
    AppLinks links,
    IErrorLog errorLog)
{
    private Guid TenantId => currentTenant.TenantId
        ?? throw new InvalidOperationException("No tenant in the current context.");

    /// <summary>מוסיף התראה למרכז ההתראות של המורה. נשמר יחד עם ה-SaveChanges הבא.</summary>
    public void AddNotification(NotificationType type, string title, string message, string? linkPath)
    {
        db.Notifications.Add(new Notification
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            Type = type,
            Title = title,
            Message = message,
            LinkPath = linkPath,
            IsRead = false,
            CreatedAt = DateTimeOffset.UtcNow
        });
    }

    /// <summary>שולח מייל למורה. כישלון שליחה נרשם ביומן התקלות ואינו מפיל את הבקשה.</summary>
    public async Task EmailAsync(string subject, string message)
    {
        var teacher = await db.Teachers.FirstOrDefaultAsync(t => t.Id == TenantId);
        if (teacher is null) return;

        var user = await userManager.FindByIdAsync(teacher.UserId);
        if (user?.Email is null) return;

        var html = EmailLayout.Render(new EmailMessage(
            subject, Intro: message, ActionLabel: "לצפייה ביומן", ActionUrl: links.TeacherLessons));

        try { await emailSender.SendAsync(user.Email, subject, html); }
        catch (Exception ex) { await errorLog.RecordAsync(ex, new ErrorOrigin { Source = "email", Path = "lessons/teacher-notification" }); }
    }
}
