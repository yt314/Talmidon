using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Talmidon.Api.Contracts;
using Talmidon.Domain;
using Talmidon.Domain.Entities;
using Talmidon.Infrastructure.Auth;
using Talmidon.Infrastructure.Data;
using Talmidon.Infrastructure.Multitenancy;

namespace Talmidon.Api.Controllers;

/// <summary>
/// פרופיל המורה עצמה: דף הכללים + הגדרות שמוצגות בספרייה הציבורית (T9).
/// Teacher ו-TeacherSubject אינם מסוננים ב-Global Query Filter (הם הדייר עצמו / מידע ציבורי),
/// לכן כל שאילתה כאן מסננת במפורש לפי TenantId (= Teacher.Id) של המורה המחוברת.
/// </summary>
[ApiController]
[Authorize(Roles = Roles.Teacher)]
[Route("api/teachers")]
public class TeachersController(TalmidonDbContext db, ICurrentTenant currentTenant) : ControllerBase
{
    private Guid TenantId => currentTenant.TenantId
        ?? throw new InvalidOperationException("No tenant in the current context.");

    [HttpGet("me")]
    public async Task<ActionResult<TeacherProfileDto>> GetMyProfile()
    {
        // הטלה של עמודות בלבד: ‎PhotoData‎ הוא blob, וטעינת הישות הייתה מושכת אותו
        // מהמסד בכל טעינת פרופיל. ‎Length‎ בתוך ההטלה מיתרגם ל-‎length()‎ של Postgres
        // ואינו קורא את התוכן עצמו.
        var row = await db.Teachers
            .Where(t => t.Id == TenantId)
            .Select(t => new
            {
                t.Id,
                t.FullName,
                t.Phone,
                t.Bio,
                t.DefaultPricePerLesson,
                t.DefaultDurationMinutes,
                t.RulesText,
                t.ContactInfo,
                t.IsPublic,
                Subjects = t.Subjects.Select(s => new SubjectDto(s.Id, s.Name)).ToList(),
                PhotoLength = t.PhotoData == null ? (int?)null : t.PhotoData.Length
            })
            .FirstOrDefaultAsync();
        if (row is null) return NotFound();

        return Ok(new TeacherProfileDto(
            row.Id, row.FullName, row.Phone, row.Bio, row.DefaultPricePerLesson,
            row.DefaultDurationMinutes, row.RulesText, row.ContactInfo, row.IsPublic,
            row.Subjects,
            row.PhotoLength,
            TeacherProfileRules.IsComplete(row.Subjects.Count, row.DefaultPricePerLesson, row.ContactInfo)));
    }

    [HttpPut("me")]
    public async Task<IActionResult> UpdateMyProfile(UpdateTeacherProfileRequest request)
    {
        var teacher = await db.Teachers.FirstOrDefaultAsync(t => t.Id == TenantId);
        if (teacher is null) return NotFound();

        teacher.Phone = request.Phone;
        teacher.Bio = request.Bio;
        teacher.DefaultPricePerLesson = request.DefaultPricePerLesson;
        teacher.DefaultDurationMinutes = request.DefaultDurationMinutes;
        teacher.RulesText = request.RulesText;
        teacher.ContactInfo = request.ContactInfo;
        teacher.IsPublic = request.IsPublic;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("me/subjects")]
    public async Task<ActionResult<SubjectDto>> AddSubject(AddSubjectRequest request)
    {
        var name = request.Name.Trim();
        if (await db.TeacherSubjects.AnyAsync(s => s.TeacherId == TenantId && s.Name == name))
            return Conflict(new { message = "התחום כבר קיים." });

        var subject = new TeacherSubject { Id = Guid.NewGuid(), TeacherId = TenantId, Name = name };
        db.TeacherSubjects.Add(subject);
        await db.SaveChangesAsync();
        return Ok(new SubjectDto(subject.Id, subject.Name));
    }

    [HttpDelete("me/subjects/{id:guid}")]
    public async Task<IActionResult> DeleteSubject(Guid id)
    {
        var subject = await db.TeacherSubjects.FirstOrDefaultAsync(s => s.Id == id && s.TeacherId == TenantId);
        if (subject is null) return NotFound();

        db.TeacherSubjects.Remove(subject);
        await db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>מחליף את כל רשימת התחומים בבת אחת. שמות ריקים וכפילויות מסוננים.</summary>
    [HttpPut("me/subjects")]
    public async Task<ActionResult<IEnumerable<SubjectDto>>> SetSubjects(SetSubjectsRequest request)
    {
        var names = request.Names
            .Select(n => n?.Trim() ?? string.Empty)
            .Where(n => n.Length > 0)
            .Where(n => n.Length <= 100)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var existing = await db.TeacherSubjects.Where(s => s.TeacherId == TenantId).ToListAsync();
        db.TeacherSubjects.RemoveRange(existing);

        var subjects = names
            .Select(n => new TeacherSubject { Id = Guid.NewGuid(), TeacherId = TenantId, Name = n })
            .ToList();
        db.TeacherSubjects.AddRange(subjects);
        await db.SaveChangesAsync();

        return Ok(subjects.Select(s => new SubjectDto(s.Id, s.Name)).ToList());
    }

    /// <summary>
    /// הצעות להשלמה אוטומטית: הקטלוג הקבוע יחד עם תחומים שמורות אחרות כבר הזינו,
    /// כך שהרשימה גדלה מעצמה עם השימוש. אינה רשימה סגורה — אפשר להקליד כל תחום.
    /// </summary>
    [HttpGet("subject-suggestions")]
    public async Task<ActionResult<IEnumerable<string>>> SubjectSuggestions()
    {
        var inUse = await db.TeacherSubjects
            .Select(s => s.Name)
            .Distinct()
            .ToListAsync();

        var all = SubjectCatalog.Common
            .Concat(inUse)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(n => n, StringComparer.CurrentCulture)
            .ToList();

        return Ok(all);
    }

    // ===== תמונת פרופיל =====

    /// <summary>גודל מרבי לתמונה. הדפדפן מקטין לריבוע לפני השליחה, ולכן זו תקרה נדיבה.</summary>
    private const int MaxPhotoBytes = 1024 * 1024;

    private static readonly string[] AllowedPhotoTypes = ["image/jpeg", "image/png", "image/webp"];

    [HttpPost("me/photo")]
    [RequestSizeLimit(MaxPhotoBytes + 8192)]
    public async Task<ActionResult<object>> UploadPhoto(IFormFile file)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { message = "לא נבחר קובץ." });
        if (file.Length > MaxPhotoBytes)
            return BadRequest(new { message = "הקובץ גדול מדי (עד 1MB)." });
        if (!AllowedPhotoTypes.Contains(file.ContentType))
            return BadRequest(new { message = "סוג קובץ לא נתמך. יש להעלות JPG, PNG או WEBP." });

        var teacher = await db.Teachers.FirstOrDefaultAsync(t => t.Id == TenantId);
        if (teacher is null) return NotFound();

        using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer);
        teacher.PhotoData = buffer.ToArray();
        teacher.PhotoContentType = file.ContentType;
        await db.SaveChangesAsync();

        return Ok(new { photoVersion = teacher.PhotoData.Length });
    }

    [HttpDelete("me/photo")]
    public async Task<IActionResult> DeletePhoto()
    {
        var teacher = await db.Teachers.FirstOrDefaultAsync(t => t.Id == TenantId);
        if (teacher is null) return NotFound();

        teacher.PhotoData = null;
        teacher.PhotoContentType = null;
        await db.SaveChangesAsync();
        return NoContent();
    }

    // ===== שעות זמינות (T) =====

    [HttpGet("me/availability")]
    public async Task<ActionResult<IEnumerable<AvailabilityWindowDto>>> GetMyAvailability()
    {
        var windows = await db.TeacherAvailabilities
            .OrderBy(a => a.DayOfWeek).ThenBy(a => a.StartTime)
            .Select(a => new AvailabilityWindowDto(
                (int)a.DayOfWeek, a.StartTime.ToString("HH:mm"), a.EndTime.ToString("HH:mm")))
            .ToListAsync();
        return Ok(windows);
    }

    /// <summary>מחליף את כל חלונות הזמינות בבת אחת (עריכת הלוח השבועי ושמירה).</summary>
    [HttpPut("me/availability")]
    public async Task<IActionResult> UpdateMyAvailability(UpdateAvailabilityRequest request)
    {
        var parsed = new List<TeacherAvailability>();
        foreach (var w in request.Windows)
        {
            if (w.DayOfWeek is < 0 or > 6)
                return BadRequest(new { message = "יום לא תקין." });
            if (!TimeOnly.TryParse(w.StartTime, out var start) || !TimeOnly.TryParse(w.EndTime, out var end))
                return BadRequest(new { message = "שעה לא תקינה." });
            if (end <= start)
                return BadRequest(new { message = "שעת הסיום חייבת להיות אחרי שעת ההתחלה." });

            parsed.Add(new TeacherAvailability
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                DayOfWeek = (DayOfWeek)w.DayOfWeek,
                StartTime = start,
                EndTime = end
            });
        }

        var existing = await db.TeacherAvailabilities.ToListAsync();
        db.TeacherAvailabilities.RemoveRange(existing);
        db.TeacherAvailabilities.AddRange(parsed);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
