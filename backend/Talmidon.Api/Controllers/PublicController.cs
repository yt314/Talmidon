using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Talmidon.Api.Contracts;
using Talmidon.Domain.Entities;
using Talmidon.Domain.Enums;
using Talmidon.Infrastructure.Data;
using Talmidon.Infrastructure.Email;
using Talmidon.Infrastructure.Identity;

namespace Talmidon.Api.Controllers;

/// <summary>ספריית המורות הציבורית (P1/P2) — ללא התחברות, ותמיד מוגבל למורות עם IsPublic=true.</summary>
[ApiController]
[AllowAnonymous]
[Route("api/public/teachers")]
public class PublicController(
    TalmidonDbContext db,
    IEmailSender emailSender,
    ILogger<PublicController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<PublicTeacherSummaryDto>>> List(
        [FromQuery] string? subject, [FromQuery] string? search)
    {
        var query = db.Teachers.Where(t => t.IsPublic);

        if (!string.IsNullOrWhiteSpace(subject))
            query = query.Where(t => t.Subjects.Any(s => s.Name == subject));
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(t => t.FullName.Contains(search));

        var teachers = await query
            .OrderBy(t => t.FullName)
            .Select(t => new PublicTeacherSummaryDto(
                t.Id, t.FullName, t.Bio, t.DefaultPricePerLesson,
                t.Subjects.Select(s => s.Name).ToList(),
                // רק אורך, לא ה-blob: אחרת כל טעינה של הספרייה הייתה מושכת את כל
                // התמונות בתוך ה-JSON. הלקוח בונה מזה את הכתובת.
                t.PhotoData == null ? (int?)null : t.PhotoData.Length))
            .ToListAsync();
        return Ok(teachers);
    }

    /// <summary>רשימת התחומים הקיימים בקרב מורות ציבוריות — לתפריט הסינון (P1).</summary>
    [HttpGet("subjects")]
    public async Task<ActionResult<IEnumerable<string>>> ListSubjects()
    {
        var subjects = await db.Teachers
            .Where(t => t.IsPublic)
            .SelectMany(t => t.Subjects.Select(s => s.Name))
            .Distinct()
            .OrderBy(s => s)
            .ToListAsync();
        return Ok(subjects);
    }

    /// <summary>
    /// תמונת הפרופיל. מוגשת רק למורה ציבורית — התמונה היא חלק מהכרטיס הציבורי,
    /// ומורה שאינה בספרייה אינה חושפת אותה. נשמרת במטמון הדפדפן לשנה, וחילוף
    /// תמונה עוקף אותו דרך פרמטר ה-v שבכתובת.
    /// </summary>
    [HttpGet("{id:guid}/photo")]
    public async Task<IActionResult> GetPhoto(Guid id)
    {
        var photo = await db.Teachers
            .Where(t => t.Id == id && t.IsPublic && t.PhotoData != null)
            .Select(t => new { t.PhotoData, t.PhotoContentType })
            .FirstOrDefaultAsync();
        if (photo is null) return NotFound();

        Response.Headers.CacheControl = "public, max-age=31536000, immutable";
        return File(photo.PhotoData!, photo.PhotoContentType ?? "image/jpeg");
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PublicTeacherDetailDto>> GetById(Guid id)
    {
        var teacher = await db.Teachers
            .Where(t => t.Id == id && t.IsPublic)
            .Select(t => new PublicTeacherDetailDto(
                t.Id, t.FullName, t.Bio, t.DefaultPricePerLesson, t.RulesText, t.ContactInfo,
                t.Subjects.Select(s => s.Name).ToList(),
                t.PhotoData == null ? (int?)null : t.PhotoData.Length))
            .FirstOrDefaultAsync();
        return teacher is null ? NotFound() : Ok(teacher);
    }

    /// <summary>
    /// פנייה מהספרייה. פתוחה למבקרים, ולכן מוגבלת בקצב לפי כתובת IP — זה טופס
    /// אנונימי שיוצר רשומות במסד ושולח מייל.
    ///
    /// ה-TenantId נכתב במפורש: אין דייר בהקשר של בקשה אנונימית, וה-DbContext
    /// מתיר הוספה כזו בדיוק כשהערך מפורש (ראו EnforceTenantOnSave).
    /// </summary>
    [HttpPost("{id:guid}/contact")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Contact(Guid id, CreateContactRequestRequest request)
    {
        var teacher = await db.Teachers
            .Where(t => t.Id == id && t.IsPublic)
            .Select(t => new { t.Id, t.FullName, t.UserId })
            .FirstOrDefaultAsync();
        if (teacher is null) return NotFound();

        var contact = new ContactRequest
        {
            Id = Guid.NewGuid(),
            TenantId = teacher.Id,
            FullName = request.FullName.Trim(),
            Phone = request.Phone.Trim(),
            Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim(),
            Subject = string.IsNullOrWhiteSpace(request.Subject) ? null : request.Subject.Trim(),
            Message = request.Message.Trim(),
            Status = ContactRequestStatus.New,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.ContactRequests.Add(contact);

        db.Notifications.Add(new Notification
        {
            Id = Guid.NewGuid(),
            TenantId = teacher.Id,
            Type = NotificationType.ContactRequest,
            Title = "פנייה חדשה מהספרייה",
            Message = $"{contact.FullName} מעוניין/ת ליצור קשר" +
                      (contact.Subject is null ? "." : $" בנושא {contact.Subject}."),
            LinkPath = "/app/contact-requests",
            IsRead = false,
            CreatedAt = DateTimeOffset.UtcNow
        });

        await db.SaveChangesAsync();

        await NotifyTeacherByEmailAsync(teacher.UserId, contact);
        return NoContent();
    }

    /// <summary>
    /// כשל בשליחת המייל אינו מפיל את הבקשה: הפנייה כבר נשמרה ומחכה במרכז
    /// ההתראות, ואין סיבה שהפונה יראה שגיאה בגלל תקלה בספק המייל.
    /// </summary>
    private async Task NotifyTeacherByEmailAsync(string teacherUserId, ContactRequest contact)
    {
        var email = await db.Users.Where(u => u.Id == teacherUserId).Select(u => u.Email).FirstOrDefaultAsync();
        if (email is null) return;

        var body = $"""
            <div dir="rtl" style="font-family:sans-serif">
              <h2>פנייה חדשה מהספרייה</h2>
              <p><strong>שם:</strong> {contact.FullName}</p>
              <p><strong>טלפון:</strong> {contact.Phone}</p>
              {(contact.Email is null ? "" : $"<p><strong>מייל:</strong> {contact.Email}</p>")}
              {(contact.Subject is null ? "" : $"<p><strong>תחום:</strong> {contact.Subject}</p>")}
              <p><strong>הודעה:</strong><br>{contact.Message}</p>
            </div>
            """;

        try { await emailSender.SendAsync(email, "פנייה חדשה מהספרייה", body); }
        catch (Exception ex) { logger.LogError(ex, "Failed to send contact request email."); }
    }
}
