using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Talmidon.Api.Contracts;
using Talmidon.Infrastructure.Auth;
using Talmidon.Infrastructure.Data;
using Talmidon.Infrastructure.Diagnostics;

namespace Talmidon.Api.Controllers.Admin;

/// <summary>יומן התקלות שנרשמו בשרת.</summary>
[ApiController]
[Route("api/admin/errors")]
[Authorize(Roles = Roles.Admin)]
public class AdminErrorsController(TalmidonDbContext db) : ControllerBase
{
    /// <summary>
    /// התקלות שנרשמו בשרת, מהחדשה לישנה. פותח על מה שעוד לא טופל — אותו היגיון כמו
    /// בהודעות מהאתר: רשימה שרק מצטברת נקראת פעם אחת ואז אף פעם.
    ///
    /// <paramref name="reference"/> הוא המסלול ההפוך: משתמשת מוסרת את הקוד שראתה על
    /// המסך, והוא מוביל ישירות לשורה — בלי קשר לתאריך או לסימון טיפול.
    /// </summary>
    [HttpGet]
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
    [HttpGet("open-count")]
    public async Task<ActionResult<AdminErrorCountDto>> OpenErrorCount() =>
        Ok(new AdminErrorCountDto(await db.ErrorLog.CountAsync(e => !e.IsHandled)));

    /// <summary>תקלה אחת עם עקבת המחסנית — נטענת רק כשפותחים אותה.</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AdminErrorDetailDto>> GetError(Guid id)
    {
        var entry = await db.ErrorLog.FirstOrDefaultAsync(e => e.Id == id);
        if (entry is null) return NotFound();

        return Ok(new AdminErrorDetailDto(
            entry.Id, entry.Reference, entry.OccurredAt, entry.Source, entry.Kind, entry.Message,
            entry.Detail, entry.Path, entry.Method, entry.UserEmail, entry.TenantId, entry.IsHandled));
    }

    [HttpPost("{id:guid}/handled")]
    public async Task<IActionResult> MarkErrorHandled(Guid id, [FromQuery] bool handled = true)
    {
        var entry = await db.ErrorLog.FirstOrDefaultAsync(e => e.Id == id);
        if (entry is null) return NotFound();

        entry.IsHandled = handled;
        await db.SaveChangesAsync();
        return NoContent();
    }
}
