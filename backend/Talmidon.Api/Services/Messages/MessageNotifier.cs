using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Talmidon.Domain.Entities;
using Talmidon.Domain.Enums;
using Talmidon.Infrastructure.Data;
using Talmidon.Infrastructure.Diagnostics;
using Talmidon.Infrastructure.Email;
using Talmidon.Infrastructure.Identity;

namespace Talmidon.Api.Services.Messages;

/// <summary>מי שומע על הודעה חדשה: המורה כשכותבים לה, והצד השני כשהיא כותבת.</summary>
public class MessageNotifier(
    TalmidonDbContext db,
    UserManager<ApplicationUser> userManager,
    IEmailSender emailSender,
    AppLinks links,
    IErrorLog errorLog,
    TeacherNotifier teacherNotifier)
{
    /// <summary>התראה למורה. נשמרת יחד עם ה-SaveChanges הבא — כלומר יחד עם ההודעה עצמה.</summary>
    public void AddTeacherNotification(string subject, string text) =>
        // כותרת ההתראה מוגבלת ל-200 תווים במסד, ונושא השיחה לבדו כבר יכול למלא אותם
        teacherNotifier.AddNotification(NotificationType.Message,
            Truncate($"הודעה חדשה: {subject}", 200), text, "/app/messages");

    public Task EmailTeacherAsync(string senderName, string subject, string text)
    {
        var title = EmailSubjects.MessageToTeacher(senderName, subject);
        var content = new EmailMessage(
            title,
            Intro: "הודעה חדשה מחכה לך בתיבה.",
            Quote: text,
            ActionLabel: "לפתיחת השיחה",
            ActionUrl: links.TeacherMessages);
        return teacherNotifier.SendEmailAsync(title, content, "messages/to-teacher");
    }

    /// <summary>שולח מייל לצד השני כשהמורה כותבת. לתלמידה בלי חשבון-מייל פשוט אין למי לשלוח.</summary>
    public async Task EmailCounterpartAsync(MessageThread thread, string studentName, string subject, string body)
    {
        var email = await CounterpartEmailAsync(thread);
        if (string.IsNullOrWhiteSpace(email)) return;

        var teacherName = await db.Teachers.Where(t => t.Id == thread.TenantId).Select(t => t.FullName).FirstOrDefaultAsync();
        var title = EmailSubjects.MessageToCounterpart(teacherName ?? "", subject);
        var html = EmailLayout.Render(new EmailMessage(
            title,
            Intro: $"בעניין {studentName}",
            Quote: body,
            ActionLabel: "לפתיחת השיחה",
            ActionUrl: thread.CounterpartRole == MessageAuthor.Parent ? links.ParentMessages : links.StudentMessages));

        try { await emailSender.SendAsync(email, title, html); }
        catch (Exception ex) { await errorLog.RecordAsync(ex, new ErrorOrigin { Source = "email", Path = "messages/to-counterpart" }); }
    }

    public static string NotificationText(string senderName, string studentName, string body)
    {
        var who = senderName == studentName ? senderName : $"{senderName} (בעניין {studentName})";
        return $"{who}: {Truncate(body, MessageThreads.PreviewLength)}";
    }

    private async Task<string?> CounterpartEmailAsync(MessageThread thread)
    {
        if (thread.CounterpartRole == MessageAuthor.Parent)
        {
            return await db.Parents.Where(p => p.Id == thread.CounterpartId)
                .Select(p => (string?)p.Email).FirstOrDefaultAsync();
        }

        var userId = await db.Students.Where(s => s.Id == thread.CounterpartId)
            .Select(s => s.UserId).FirstOrDefaultAsync();
        return userId is null ? null : (await userManager.FindByIdAsync(userId))?.Email;
    }

    private static string Truncate(string text, int max) =>
        text.Length <= max ? text : text[..(max - 1)] + "…";
}
