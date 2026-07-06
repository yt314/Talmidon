using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Talmidon.Api.Contracts;
using Talmidon.Domain.Entities;
using Talmidon.Infrastructure.Auth;
using Talmidon.Infrastructure.Data;
using Talmidon.Infrastructure.Email;
using Talmidon.Infrastructure.Multitenancy;

namespace Talmidon.Api.Controllers;

/// <summary>
/// תשלומים: חיוב לפי שיעור (לא מנוי חודשי). המורה בוחרת שיעורים פתוחים של הורה מסוים
/// ומסמנת אותם כ"שולם" ביחד — נוצר Payment אחד שמכסה אותם ונשלח מייל אישור להורה (T7/R3).
/// </summary>
[ApiController]
[Route("api/payments")]
public class PaymentsController(
    TalmidonDbContext db,
    ICurrentTenant currentTenant,
    IEmailSender emailSender,
    ILogger<PaymentsController> logger) : ControllerBase
{
    private Guid TenantId => currentTenant.TenantId
        ?? throw new InvalidOperationException("No tenant in the current context.");

    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new InvalidOperationException("No user id in the current context.");

    private Task<Parent?> CurrentParentAsync() => db.Parents.FirstOrDefaultAsync(p => p.UserId == CurrentUserId);

    // ===== מורה =====

    [Authorize(Roles = Roles.Teacher)]
    [HttpGet("open-charges")]
    public async Task<ActionResult<IEnumerable<OpenChargeDto>>> OpenCharges(
        [FromQuery] Guid? studentId, [FromQuery] Guid? parentId)
    {
        var query = db.Lessons.Where(l => l.PaymentRequired && l.PaymentId == null);
        if (studentId is not null) query = query.Where(l => l.StudentId == studentId);
        if (parentId is not null)
        {
            var childIds = db.StudentParents.Where(sp => sp.ParentId == parentId).Select(sp => sp.StudentId);
            query = query.Where(l => childIds.Contains(l.StudentId));
        }

        var charges = await query
            .OrderBy(l => l.StartTime)
            .Select(l => new OpenChargeDto(l.Id, l.StudentId, l.Student.FullName, l.StartTime, l.Amount))
            .ToListAsync();
        return Ok(charges);
    }

    [Authorize(Roles = Roles.Teacher)]
    [HttpGet]
    public async Task<ActionResult<IEnumerable<PaymentDto>>> List([FromQuery] Guid? parentId)
    {
        var query = db.Payments.AsQueryable();
        if (parentId is not null) query = query.Where(p => p.ParentId == parentId);

        var payments = await query
            .OrderByDescending(p => p.PaidDate)
            .Select(p => new PaymentDto(
                p.Id, p.ParentId, p.Parent.FullName, p.Amount, p.PaidDate, p.Method, p.Note,
                p.CoveredLessons.Count, p.ConfirmationSentAt))
            .ToListAsync();
        return Ok(payments);
    }

    [Authorize(Roles = Roles.Teacher)]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PaymentDetailDto>> GetById(Guid id)
    {
        var payment = await db.Payments
            .Where(p => p.Id == id)
            .Select(p => new PaymentDetailDto(
                p.Id, p.ParentId, p.Parent.FullName, p.Amount, p.PaidDate, p.Method, p.Note, p.ConfirmationSentAt,
                p.CoveredLessons
                    .Select(l => new PaymentLessonDto(l.Id, l.StudentId, l.Student.FullName, l.StartTime, l.Amount))
                    .ToList()))
            .FirstOrDefaultAsync();
        return payment is null ? NotFound() : Ok(payment);
    }

    /// <summary>מסמנת שיעורים פתוחים כ"שולם" ע"י הורה מסוים, ושולחת מייל אישור.</summary>
    [Authorize(Roles = Roles.Teacher)]
    [HttpPost]
    public async Task<ActionResult<PaymentDto>> Create(CreatePaymentRequest request)
    {
        var parent = await db.Parents.FirstOrDefaultAsync(p => p.Id == request.ParentId);
        if (parent is null) return NotFound(new { message = "הורה לא נמצא." });

        var lessonIds = request.LessonIds.Distinct().ToList();
        var lessons = await db.Lessons.Include(l => l.Student)
            .Where(l => lessonIds.Contains(l.Id))
            .ToListAsync();

        if (lessons.Count != lessonIds.Count)
            return NotFound(new { message = "אחד או יותר מהשיעורים לא נמצאו." });
        if (lessons.Any(l => !l.PaymentRequired || l.PaymentId != null))
            return Conflict(new { message = "ניתן לשלם רק על שיעורים פתוחים לתשלום." });

        var childIds = await db.StudentParents
            .Where(sp => sp.ParentId == parent.Id)
            .Select(sp => sp.StudentId)
            .ToListAsync();
        if (lessons.Any(l => !childIds.Contains(l.StudentId)))
            return BadRequest(new { message = "השיעורים חייבים להיות של ילדי ההורה הנבחר." });

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

        if (await SendPaymentConfirmationAsync(parent, payment.Amount, payment.PaidDate, lessons))
        {
            payment.ConfirmationSentAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
        }

        return CreatedAtAction(nameof(GetById), new { id = payment.Id },
            new PaymentDto(payment.Id, parent.Id, parent.FullName, payment.Amount, payment.PaidDate,
                payment.Method, payment.Note, lessons.Count, payment.ConfirmationSentAt));
    }

    /// <summary>ביטול סימון תשלום בטעות: משחרר את השיעורים חזרה לפתוחים ומוחק את הרשומה.</summary>
    [Authorize(Roles = Roles.Teacher)]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var payment = await db.Payments.FirstOrDefaultAsync(p => p.Id == id);
        if (payment is null) return NotFound();

        var coveredLessons = await db.Lessons.Where(l => l.PaymentId == id).ToListAsync();
        foreach (var lesson in coveredLessons)
            lesson.PaymentId = null;

        db.Payments.Remove(payment);
        await db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>
    /// שולחת תזכורת תשלום לכל הורה עם חיובים פתוחים (מרוכזת לפי ילד).
    /// כרגע מופעלת ע"י המורה ידנית; חיבור לתזמון אוטומטי חודשי (Hangfire/Quartz) נדרש בהמשך.
    /// </summary>
    [Authorize(Roles = Roles.Teacher)]
    [HttpPost("send-monthly-reminders")]
    public async Task<ActionResult<object>> SendMonthlyReminders()
    {
        var openCharges = await (
            from l in db.Lessons
            where l.PaymentRequired && l.PaymentId == null
            join sp in db.StudentParents on l.StudentId equals sp.StudentId
            select new { sp.Parent, l.Student.FullName, l.StartTime, l.Amount })
            .ToListAsync();

        var sentCount = 0;
        foreach (var group in openCharges.GroupBy(x => x.Parent))
        {
            var charges = group.Select(g => (g.FullName, g.StartTime, g.Amount)).ToList();
            if (await SendMonthlyReminderAsync(group.Key, charges))
                sentCount++;
        }

        return Ok(new { sentCount });
    }

    // ===== הורה (R3) =====

    [Authorize(Roles = Roles.Parent)]
    [HttpGet("mine/open-charges")]
    public async Task<ActionResult<IEnumerable<OpenChargeDto>>> MyOpenCharges()
    {
        var parent = await CurrentParentAsync();
        if (parent is null) return Forbid();

        var childIds = db.StudentParents.Where(sp => sp.ParentId == parent.Id).Select(sp => sp.StudentId);
        var charges = await db.Lessons
            .Where(l => l.PaymentRequired && l.PaymentId == null && childIds.Contains(l.StudentId))
            .OrderBy(l => l.StartTime)
            .Select(l => new OpenChargeDto(l.Id, l.StudentId, l.Student.FullName, l.StartTime, l.Amount))
            .ToListAsync();
        return Ok(charges);
    }

    [Authorize(Roles = Roles.Parent)]
    [HttpGet("mine")]
    public async Task<ActionResult<IEnumerable<PaymentDto>>> MyPayments()
    {
        var parent = await CurrentParentAsync();
        if (parent is null) return Forbid();

        var payments = await db.Payments
            .Where(p => p.ParentId == parent.Id)
            .OrderByDescending(p => p.PaidDate)
            .Select(p => new PaymentDto(
                p.Id, p.ParentId, parent.FullName, p.Amount, p.PaidDate, p.Method, p.Note,
                p.CoveredLessons.Count, p.ConfirmationSentAt))
            .ToListAsync();
        return Ok(payments);
    }

    // ----- עזר -----

    private static string BuildEmailHtml(string title, string greeting, string introLine, IEnumerable<string> lines) =>
        $"""
        <div dir="rtl" style="font-family:Arial,sans-serif">
          <h2>{WebUtility.HtmlEncode(title)}</h2>
          <p>{WebUtility.HtmlEncode(greeting)}</p>
          <p>{WebUtility.HtmlEncode(introLine)}</p>
          <ul>{string.Join("", lines)}</ul>
        </div>
        """;

    private async Task<bool> SendPaymentConfirmationAsync(
        Parent parent, decimal amount, DateOnly paidDate, List<Lesson> lessons)
    {
        var lines = lessons.Select(l =>
            $"<li>{WebUtility.HtmlEncode(l.Student.FullName)} — {l.StartTime:dd/MM/yyyy} — ₪{l.Amount}</li>");
        var html = BuildEmailHtml(
            "אישור קבלת תשלום",
            $"שלום {parent.FullName},",
            $"התקבל תשלום בסך ₪{amount} בתאריך {paidDate:dd/MM/yyyy}, המכסה את השיעורים הבאים:",
            lines);

        try
        {
            await emailSender.SendAsync(parent.Email, "אישור קבלת תשלום", html);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send payment confirmation email.");
            return false;
        }
    }

    private async Task<bool> SendMonthlyReminderAsync(
        Parent parent, List<(string StudentName, DateTimeOffset StartTime, decimal Amount)> charges)
    {
        var total = charges.Sum(c => c.Amount);
        var lines = charges.Select(c =>
            $"<li>{WebUtility.HtmlEncode(c.StudentName)} — {c.StartTime:dd/MM/yyyy} — ₪{c.Amount}</li>");
        var html = BuildEmailHtml(
            "תזכורת תשלום חודשית",
            $"שלום {parent.FullName},",
            $"להלן החיובים הפתוחים לתשלום, בסך כולל של ₪{total}:",
            lines);

        try
        {
            await emailSender.SendAsync(parent.Email, "תזכורת תשלום חודשית", html);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send monthly payment reminder email.");
            return false;
        }
    }
}
