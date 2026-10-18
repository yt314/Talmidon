using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Talmidon.Api.Contracts;
using Talmidon.Domain;
using Talmidon.Domain.Entities;
using Talmidon.Infrastructure.Auth;
using Talmidon.Infrastructure.Data;
using Talmidon.Infrastructure.Diagnostics;
using Talmidon.Infrastructure.Identity;

namespace Talmidon.Api.Controllers;

/// <summary>
/// תפקיד-העל המינימלי לתחזוקת הפלטפורמה (ראו נספח האפיון): חוצה-דיירים במפורש (אין TenantId
/// למנהל — לכן כל שאילתה כאן על ישויות בבעלות-מורה משתמשת ב-IgnoreQueryFilters). היקף מכוון:
/// רשימת מורות + נעילה/שחרור של חשבון (למשל בעקבות תלונה) דרך מנגנון ה-Lockout הקיים ב-Identity —
/// אין כאן שכפול/הרחבה של מודל ההרשאות.
/// </summary>
[ApiController]
[Route("api/admin")]
[Authorize(Roles = Roles.Admin)]
public class AdminController(TalmidonDbContext db, UserManager<ApplicationUser> userManager) : ControllerBase
{
    [HttpGet("teachers")]
    public async Task<ActionResult<List<AdminTeacherDto>>> ListTeachers()
    {
        var teachers = await db.Teachers
            .OrderBy(t => t.CreatedAt)
            .Select(t => new
            {
                t.Id,
                t.UserId,
                t.FullName,
                t.CreatedAt,
                t.IsPublic,
                StudentCount = db.Students.IgnoreQueryFilters().Count(s => s.TenantId == t.Id)
            })
            .ToListAsync();

        var userIds = teachers.Select(t => t.UserId).ToList();
        var users = await db.Users
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Email, u.LockoutEnd })
            .ToDictionaryAsync(u => u.Id);

        var now = DateTimeOffset.UtcNow;
        var result = teachers.Select(t =>
        {
            users.TryGetValue(t.UserId, out var user);
            var isLockedOut = user?.LockoutEnd is { } end && end > now;
            return new AdminTeacherDto(t.Id, t.FullName, user?.Email ?? "", t.CreatedAt, t.IsPublic, t.StudentCount, isLockedOut);
        }).ToList();

        return Ok(result);
    }

    /// <summary>נועלת את חשבון ההתחברות של המורה (למשל בעקבות תלונה) — משתמשת במנגנון ה-Lockout הקיים של Identity, כך שהתחברות חדשה נחסמת מיידית.</summary>
    [HttpPost("teachers/{id:guid}/lock")]
    public async Task<IActionResult> LockTeacher(Guid id)
    {
        var user = await FindTeacherUserAsync(id);
        if (user is null) return NotFound();

        await userManager.SetLockoutEnabledAsync(user, true);
        await userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);
        return NoContent();
    }

    /// <summary>משחררת נעילה שהוטלה על חשבון מורה.</summary>
    [HttpPost("teachers/{id:guid}/unlock")]
    public async Task<IActionResult> UnlockTeacher(Guid id)
    {
        var user = await FindTeacherUserAsync(id);
        if (user is null) return NotFound();

        await userManager.SetLockoutEndDateAsync(user, null);
        return NoContent();
    }

    private async Task<ApplicationUser?> FindTeacherUserAsync(Guid teacherId)
    {
        var teacher = await db.Teachers.FirstOrDefaultAsync(t => t.Id == teacherId);
        return teacher is null ? null : await userManager.FindByIdAsync(teacher.UserId);
    }

    // ===== ניהול הצעות תחומי לימוד =====

    /// <summary>
    /// רשימת ההצעות כפי שמורה רואה אותן, עם סימון מה ניתן להסתיר. הקטלוג הקבוע
    /// מסומן בנפרד כי הוא חלק מהקוד ולא ניתן להסרה מכאן.
    /// </summary>
    [HttpGet("subject-suggestions")]
    public async Task<ActionResult<IEnumerable<AdminSubjectSuggestionDto>>> ListSubjectSuggestions()
    {
        var inUse = await db.TeacherSubjects
            .Select(s => s.Name)
            .Distinct()
            .ToListAsync();
        var curated = await db.SubjectSuggestions.ToListAsync();

        var hidden = curated
            .Where(x => x.IsHidden)
            .Select(x => x.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var builtIn = SubjectCatalog.Common.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var rows = SubjectCatalog.Common
            .Concat(inUse)
            .Concat(curated.Select(x => x.Name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(n => n, StringComparer.CurrentCulture)
            .Select(name => new AdminSubjectSuggestionDto(
                name,
                hidden.Contains(name),
                builtIn.Contains(name),
                inUse.Contains(name, StringComparer.OrdinalIgnoreCase)))
            .ToList();

        return Ok(rows);
    }

    [HttpPost("subject-suggestions")]
    public async Task<IActionResult> AddSubjectSuggestion(AdminSubjectSuggestionRequest request)
    {
        var name = request.Name.Trim();
        if (name.Length == 0) return BadRequest(new { message = "שם ריק." });

        var existing = await FindSuggestionAsync(name);
        if (existing is null)
        {
            db.SubjectSuggestions.Add(new SubjectSuggestion
            {
                Id = Guid.NewGuid(),
                Name = name,
                IsHidden = false,
                CreatedAt = DateTimeOffset.UtcNow
            });
        }
        else
        {
            // הוספה מחדש של שם שהוסתר פשוט מבטלת את ההסתרה
            existing.IsHidden = false;
        }

        await db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>
    /// מסתיר שם מרשימת ההצעות. אינו נוגע בפרופילים: מורה שכבר בחרה בתחום שומרת
    /// אותו, והוא פשוט לא מוצע יותר לאחרות.
    /// </summary>
    [HttpDelete("subject-suggestions")]
    public async Task<IActionResult> HideSubjectSuggestion([FromQuery] string name)
    {
        var trimmed = (name ?? string.Empty).Trim();
        if (trimmed.Length == 0) return BadRequest(new { message = "שם ריק." });
        if (SubjectCatalog.Common.Contains(trimmed, StringComparer.OrdinalIgnoreCase))
            return BadRequest(new { message = "תחום מהקטלוג הקבוע אינו ניתן להסתרה." });

        var existing = await FindSuggestionAsync(trimmed);
        if (existing is null)
        {
            db.SubjectSuggestions.Add(new SubjectSuggestion
            {
                Id = Guid.NewGuid(),
                Name = trimmed,
                IsHidden = true,
                CreatedAt = DateTimeOffset.UtcNow
            });
        }
        else
        {
            existing.IsHidden = true;
        }

        await db.SaveChangesAsync();
        return NoContent();
    }

    private async Task<SubjectSuggestion?> FindSuggestionAsync(string name) =>
        await db.SubjectSuggestions.FirstOrDefaultAsync(x => x.Name.ToLower() == name.ToLower());

    // ===== הודעות מהאתר (תקופת ההרצה) =====

    /// <summary>פותח על מה שעוד לא טופל, מהחדש לישן — סדר הקריאה בפועל.</summary>
    [HttpGet("feedback")]
    public async Task<ActionResult<IEnumerable<SiteFeedbackDto>>> ListFeedback([FromQuery] bool includeHandled = false)
    {
        var query = db.SiteFeedback.AsQueryable();
        if (!includeHandled) query = query.Where(f => !f.IsHandled);

        var rows = await query
            .OrderByDescending(f => f.CreatedAt)
            .Take(200)
            .Select(f => new SiteFeedbackDto(f.Id, f.Message, f.ContactInfo, f.PageUrl, f.IsHandled, f.CreatedAt))
            .ToListAsync();

        return Ok(rows);
    }

    [HttpPost("feedback/{id:guid}/handled")]
    public async Task<IActionResult> MarkFeedbackHandled(Guid id, [FromQuery] bool handled = true)
    {
        var feedback = await db.SiteFeedback.FirstOrDefaultAsync(f => f.Id == id);
        if (feedback is null) return NotFound();

        feedback.IsHandled = handled;
        await db.SaveChangesAsync();
        return NoContent();
    }

    // ===== יומן התקלות =====

    /// <summary>
    /// התקלות שנרשמו בשרת, מהחדשה לישנה. פותח על מה שעוד לא טופל — אותו היגיון כמו
    /// בהודעות מהאתר: רשימה שרק מצטברת נקראת פעם אחת ואז אף פעם.
    ///
    /// <paramref name="reference"/> הוא המסלול ההפוך: משתמשת מוסרת את הקוד שראתה על
    /// המסך, והוא מוביל ישירות לשורה — בלי קשר לתאריך או לסימון טיפול.
    /// </summary>
    [HttpGet("errors")]
    public async Task<ActionResult<IEnumerable<AdminErrorDto>>> ListErrors(
        [FromQuery] bool includeHandled = false,
        [FromQuery] string? reference = null)
    {
        var query = db.ErrorLog.AsQueryable();

        var wanted = ErrorReference.Normalize(reference);
        if (wanted.Length > 0)
            query = query.Where(e => e.Reference == wanted);
        else if (!includeHandled)
            query = query.Where(e => !e.IsHandled);

        var rows = await query
            .OrderByDescending(e => e.OccurredAt)
            .Take(100)
            .Select(e => new AdminErrorDto(
                e.Id, e.Reference, e.OccurredAt, e.Source, e.Kind, e.Message,
                e.Path, e.Method, e.UserEmail, e.IsHandled))
            .ToListAsync();

        return Ok(rows);
    }

    /// <summary>
    /// כמה תקלות פתוחות יש. קיים כדי שמסך הניהול יוכל להציג מספר ליד "תקלות"
    /// בלי לטעון את הרשימה כולה בכל מסך.
    ///
    /// הסיבה שזה קיים בכלל: יומן שצריך להיכנס אליו כדי לדעת שיש בו משהו נקרא
    /// פעם אחת ואז אף פעם. מספר שמופיע מעצמו הוא ההבדל בין יומן לבין התראה.
    /// </summary>
    [HttpGet("errors/open-count")]
    public async Task<ActionResult<AdminErrorCountDto>> OpenErrorCount() =>
        Ok(new AdminErrorCountDto(await db.ErrorLog.CountAsync(e => !e.IsHandled)));

    /// <summary>תקלה אחת עם עקבת המחסנית — נטענת רק כשפותחים אותה.</summary>
    [HttpGet("errors/{id:guid}")]
    public async Task<ActionResult<AdminErrorDetailDto>> GetError(Guid id)
    {
        var entry = await db.ErrorLog.FirstOrDefaultAsync(e => e.Id == id);
        if (entry is null) return NotFound();

        return Ok(new AdminErrorDetailDto(
            entry.Id, entry.Reference, entry.OccurredAt, entry.Source, entry.Kind, entry.Message,
            entry.Detail, entry.Path, entry.Method, entry.UserEmail, entry.TenantId, entry.IsHandled));
    }

    [HttpPost("errors/{id:guid}/handled")]
    public async Task<IActionResult> MarkErrorHandled(Guid id, [FromQuery] bool handled = true)
    {
        var entry = await db.ErrorLog.FirstOrDefaultAsync(e => e.Id == id);
        if (entry is null) return NotFound();

        entry.IsHandled = handled;
        await db.SaveChangesAsync();
        return NoContent();
    }
}
