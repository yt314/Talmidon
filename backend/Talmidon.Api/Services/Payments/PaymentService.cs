using Microsoft.EntityFrameworkCore;
using Talmidon.Api.Contracts;
using Talmidon.Domain.Entities;
using Talmidon.Infrastructure.Data;
using Talmidon.Infrastructure.Diagnostics;
using Talmidon.Infrastructure.Email;
using Talmidon.Infrastructure.Multitenancy;

namespace Talmidon.Api.Services.Payments;

/// <summary>
/// תשלומים: חיוב לפי שיעור (לא מנוי חודשי). המורה בוחרת שיעורים פתוחים של הורה מסוים
/// ומסמנת אותם כ"שולם" ביחד — נוצר Payment אחד שמכסה אותם ונשלח מייל אישור להורה (T7/R3).
/// </summary>
public class PaymentService(
    TalmidonDbContext db,
    ICurrentTenant currentTenant,
    IEmailSender emailSender,
    AppLinks links,
    IErrorLog errorLog)
{
    private Guid TenantId => currentTenant.TenantId
        ?? throw new InvalidOperationException("No tenant in the current context.");

    /// <summary>מסמנת שיעורים פתוחים כ"שולם" ע"י הורה מסוים, ושולחת מייל אישור.</summary>
    public async Task<ServiceResult<PaymentDto>> CreateAsync(CreatePaymentRequest request)
    {
        var parent = await db.Parents.FirstOrDefaultAsync(p => p.Id == request.ParentId);
        if (parent is null) return ServiceError.NotFound("הורה לא נמצא.");

        var lessonIds = request.LessonIds.Distinct().ToList();
        var lessons = await db.Lessons.Include(l => l.Student)
            .Where(l => lessonIds.Contains(l.Id))
            .ToListAsync();

        var invalid = await ValidateLessonsAsync(parent, lessonIds, lessons);
        if (invalid is not null) return invalid;

        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            ParentId = parent.Id,
            Amount = lessons.Sum(l => l.Amount),
            PaidDate = request.PaidDate,
            Method = request.Method,
            Note = request.Note
        };
        db.Payments.Add(payment);
        foreach (var lesson in lessons)
            lesson.PaymentId = payment.Id;
        await db.SaveChangesAsync();

        if (await SendReceiptAsync(parent, payment, lessons))
        {
            payment.ConfirmationSentAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
        }

        return new PaymentDto(payment.Id, parent.Id, parent.FullName, payment.Amount, payment.PaidDate,
            payment.Method, payment.Note, lessons.Count, payment.ConfirmationSentAt);
    }

    /// <summary>ביטול סימון תשלום בטעות: משחרר את השיעורים חזרה לפתוחים ומוחק את הרשומה.</summary>
    public async Task<ServiceError?> DeleteAsync(Guid id)
    {
        var payment = await db.Payments.FirstOrDefaultAsync(p => p.Id == id);
        if (payment is null) return ServiceError.NotFound();

        var coveredLessons = await db.Lessons.Where(l => l.PaymentId == id).ToListAsync();
        foreach (var lesson in coveredLessons)
            lesson.PaymentId = null;

        db.Payments.Remove(payment);
        await db.SaveChangesAsync();
        return null;
    }

    /// <summary>כל השיעורים קיימים, פתוחים לתשלום, ושל ילדי ההורה שנבחר כמשלם.</summary>
    private async Task<ServiceError?> ValidateLessonsAsync(Parent parent, List<Guid> lessonIds, List<Lesson> lessons)
    {
        if (lessons.Count != lessonIds.Count)
            return ServiceError.NotFound("אחד או יותר מהשיעורים לא נמצאו.");
        if (lessons.Any(l => !l.PaymentRequired || l.PaymentId != null))
            return ServiceError.Conflict("ניתן לשלם רק על שיעורים פתוחים לתשלום.");

        var childIds = await db.StudentParents
            .Where(sp => sp.ParentId == parent.Id)
            .Select(sp => sp.StudentId)
            .ToListAsync();
        return lessons.Any(l => !childIds.Contains(l.StudentId))
            ? ServiceError.Invalid("השיעורים חייבים להיות של ילדי ההורה הנבחר.")
            : null;
    }

    /// <summary>מייל אישור להורה. true אם יצא — וזה מה שנרשם כ-ConfirmationSentAt.</summary>
    private async Task<bool> SendReceiptAsync(Parent parent, Payment payment, List<Lesson> lessons)
    {
        var lines = lessons
            .Select(l => $"{l.Student.FullName} — {EmailTime.Date(l.StartTime)} — ₪{l.Amount}")
            .ToList();
        var html = EmailLayout.Render(new EmailMessage(
            "אישור קבלת תשלום",
            Greeting: $"שלום {parent.FullName},",
            Intro: $"התקבל תשלום בסך ₪{payment.Amount} בתאריך {payment.PaidDate:dd/MM/yyyy}, המכסה את השיעורים הבאים:",
            Items: lines,
            ActionLabel: "לצפייה בתשלומים",
            ActionUrl: links.ParentPayments));

        try
        {
            await emailSender.SendAsync(parent.Email, EmailSubjects.PaymentReceived(payment.Amount, payment.PaidDate), html);
            return true;
        }
        catch (Exception ex)
        {
            await errorLog.RecordAsync(ex, new ErrorOrigin { Source = "email", Path = "payments/receipt" });
            return false;
        }
    }
}
