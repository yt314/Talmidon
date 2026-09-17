using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Talmidon.Infrastructure.Data;
using Talmidon.Infrastructure.Email;
using Talmidon.Infrastructure.Identity;

namespace Talmidon.Api.Services;

/// <summary>
/// שולח למשפחה של תלמיד/ה על החלטות המורה: ההורים, ובמקרים שנוגעים ליומן —
/// גם התלמידה עצמה.
///
/// במקום אחד ולא כשיטות פרטיות בכל בקר: הכלל "מי מקבל על מה" נשבר בדיוק כשהוא
/// מועתק. פעולות שנגעו באותה משפחה מבקרים שונים כבר הודיעו לקהלים שונים, וחלקן
/// לא הודיעו כלל.
/// </summary>
public class FamilyNotifier(
    TalmidonDbContext db,
    UserManager<ApplicationUser> userManager,
    IEmailSender emailSender,
    AppLinks links,
    ILogger<FamilyNotifier> logger)
{
    /// <summary>ההורים בלבד — תשובה לפנייה שלהם, או מידע שאינו משנה את היומן של התלמידה.</summary>
    public async Task NotifyParentsAsync(Guid studentId, string subject, string message, IReadOnlyList<string>? items = null)
    {
        var parentEmails = await db.StudentParents
            .Where(sp => sp.StudentId == studentId)
            .Select(sp => sp.Parent.Email)
            .ToListAsync();

        var html = Render(subject, message, items, links.ParentLessons);
        foreach (var email in parentEmails) await SendAsync(email, subject, html, "parent");
    }

    /// <summary>התלמידה בלבד. תלמידה צעירה עשויה לא להיות מקושרת לחשבון — ואז אין למי לשלוח.</summary>
    public async Task NotifyStudentAsync(Guid studentId, string subject, string message, IReadOnlyList<string>? items = null)
    {
        var userId = await db.Students.Where(s => s.Id == studentId).Select(s => s.UserId).FirstOrDefaultAsync();
        if (userId is null) return;

        var user = await userManager.FindByIdAsync(userId);
        if (user?.Email is null) return;

        await SendAsync(user.Email, subject, Render(subject, message, items, links.StudentLessons), "student");
    }

    /// <summary>
    /// כל מי שהעניין נוגע לה: ההורים והתלמידה.
    ///
    /// משמש כשהיומן עצמו השתנה — תלמידה יכולה לבקש שיעור בעצמה, ומייל להורה
    /// אינו מגיע אליה.
    /// </summary>
    public async Task NotifyFamilyAsync(Guid studentId, string subject, string message, IReadOnlyList<string>? items = null)
    {
        await NotifyParentsAsync(studentId, subject, message, items);
        await NotifyStudentAsync(studentId, subject, message, items);
    }

    private static string Render(string subject, string message, IReadOnlyList<string>? items, string? actionUrl) =>
        EmailLayout.Render(new EmailMessage(
            subject, Intro: message, Items: items, ActionLabel: "לצפייה ביומן", ActionUrl: actionUrl));

    /// <summary>כישלון שליחה אינו מבטל את הפעולה שכבר נשמרה — הוא נרשם ביומן וזהו.</summary>
    private async Task SendAsync(string email, string subject, string html, string audience)
    {
        try { await emailSender.SendAsync(email, subject, html); }
        catch (Exception ex) { logger.LogError(ex, "Failed to send {Audience} notification email.", audience); }
    }
}
