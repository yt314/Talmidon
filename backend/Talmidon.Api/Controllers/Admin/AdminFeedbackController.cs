using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Talmidon.Api.Contracts;
using Talmidon.Infrastructure.Auth;
using Talmidon.Infrastructure.Data;

namespace Talmidon.Api.Controllers.Admin;

/// <summary>הודעות מהאתר (תקופת ההרצה).</summary>
[ApiController]
[Route("api/admin/feedback")]
[Authorize(Roles = Roles.Admin)]
public class AdminFeedbackController(TalmidonDbContext db) : ControllerBase
{
    /// <summary>פותח על מה שעוד לא טופל, מהחדש לישן — סדר הקריאה בפועל.</summary>
    [HttpGet]
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

    [HttpPost("{id:guid}/handled")]
    public async Task<IActionResult> MarkFeedbackHandled(Guid id, [FromQuery] bool handled = true)
    {
        var feedback = await db.SiteFeedback.FirstOrDefaultAsync(f => f.Id == id);
        if (feedback is null) return NotFound();

        feedback.IsHandled = handled;
        await db.SaveChangesAsync();
        return NoContent();
    }
}
