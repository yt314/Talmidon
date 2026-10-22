using Microsoft.EntityFrameworkCore;
using Talmidon.Api.Contracts;
using Talmidon.Domain.Entities;
using Talmidon.Domain.Enums;
using Talmidon.Infrastructure.Data;
using Talmidon.Infrastructure.Diagnostics;
using Talmidon.Infrastructure.Email;

namespace Talmidon.Api.Services.Public;

/// <summary>
/// פנייה מהספרייה הציבורית: נשמרת אצל המורה, עם התראה במרכז ההתראות ומייל.
///
/// ה-TenantId נכתב במפורש: אין דייר בהקשר של בקשה אנונימית, וה-DbContext מתיר
/// הוספה כזו בדיוק כשהערך מפורש (ראו EnforceTenantOnSave).
/// </summary>
public class ContactRequestIntake(
    TalmidonDbContext db,
    IEmailSender emailSender,
    AppLinks links,
    IErrorLog errorLog)
{
    public async Task<ServiceError?> SubmitAsync(Guid teacherId, CreateContactRequestRequest request)
    {
        var teacher = await db.Teachers
            .Where(t => t.Id == teacherId && t.IsPublic)
            .Select(t => new { t.Id, t.UserId, t.AcceptingStudents })
            .FirstOrDefaultAsync();
        if (teacher is null) return ServiceError.NotFound();

        // The card says so and the form is closed, but a page left open since
        // before she switched it off would still post. The teacher was promised
        // that turning this off stops the enquiries, so it has to hold here too.
        if (!teacher.AcceptingStudents)
            return ServiceError.Conflict("המורה אינה מקבלת תלמידות חדשות כרגע, ולכן לא ניתן לשלוח אליה פנייה.");

        var contact = NewContactRequest(teacher.Id, request);
        db.ContactRequests.Add(contact);
        db.Notifications.Add(NewNotification(contact));
        await db.SaveChangesAsync();

        await EmailTeacherAsync(teacher.UserId, contact);
        return null;
    }

    private static ContactRequest NewContactRequest(Guid teacherId, CreateContactRequestRequest request) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = teacherId,
        FullName = request.FullName.Trim(),
        Phone = request.Phone.Trim(),
        Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim(),
        Subject = string.IsNullOrWhiteSpace(request.Subject) ? null : request.Subject.Trim(),
        Message = request.Message.Trim(),
        Status = ContactRequestStatus.New,
        CreatedAt = DateTimeOffset.UtcNow
    };

    private static Notification NewNotification(ContactRequest contact) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = contact.TenantId,
        Type = NotificationType.ContactRequest,
        Title = "פנייה חדשה מהספרייה",
        Message = $"{contact.FullName} מעוניין/ת ליצור קשר" + (contact.Subject is null ? "." : $" בנושא {contact.Subject}."),
        LinkPath = "/app/contact-requests",
        IsRead = false,
        CreatedAt = DateTimeOffset.UtcNow
    };

    /// <summary>
    /// כשל בשליחת המייל אינו מפיל את הבקשה: הפנייה כבר נשמרה ומחכה במרכז
    /// ההתראות, ואין סיבה שהפונה יראה שגיאה בגלל תקלה בספק המייל.
    /// </summary>
    private async Task EmailTeacherAsync(string teacherUserId, ContactRequest contact)
    {
        var email = await db.Users.Where(u => u.Id == teacherUserId).Select(u => u.Email).FirstOrDefaultAsync();
        if (email is null) return;

        var body = EmailLayout.Render(new EmailMessage(
            "פנייה חדשה מהספרייה",
            Intro: "התקבלה פנייה דרך הכרטיס הציבורי שלך.",
            Details:
            [
                ("שם", contact.FullName),
                ("טלפון", contact.Phone),
                ("מייל", contact.Email ?? ""),
                ("תחום", contact.Subject ?? "")
            ],
            Quote: contact.Message,
            ActionLabel: "לצפייה בפנייה",
            ActionUrl: links.TeacherContactRequests));

        var subject = EmailSubjects.ContactRequest(contact.FullName, contact.Subject);
        try { await emailSender.SendAsync(email, subject, body); }
        // פנייה של תלמידה מתעניינת ששקעה בדרך למורה היא בדיוק מה שאסור לאבד בשקט.
        catch (Exception ex) { await errorLog.RecordAsync(ex, new ErrorOrigin { Source = "email", Path = "public/contact-request" }); }
    }
}
