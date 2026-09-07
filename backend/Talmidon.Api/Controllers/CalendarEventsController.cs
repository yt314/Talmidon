using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Talmidon.Api.Contracts;
using Talmidon.Domain.Entities;
using Talmidon.Infrastructure.Data;
using Talmidon.Infrastructure.Identity;
using Talmidon.Infrastructure.Multitenancy;

namespace Talmidon.Api.Controllers;

/// <summary>
/// אירועים ביומן שאינם שיעורים — פגישה, חופשה, יום חסום.
///
/// למורה בלבד: אלה אירועים פרטיים שלה, ואין להם מה להופיע בפורטל של הורה או תלמיד.
/// הבידוד בין דיירות נאכף ממילא ע"י ה-Global Query Filter, ולכן החיפוש כאן לפי Id בלבד
/// אינו יכול להחזיר אירוע של מורה אחרת.
/// </summary>
[ApiController]
[Authorize(Roles = Roles.Teacher)]
[Route("api/calendar-events")]
public class CalendarEventsController(TalmidonDbContext db, ICurrentTenant currentTenant) : ControllerBase
{
    private Guid TenantId => currentTenant.TenantId
        ?? throw new InvalidOperationException("No tenant in the current context.");

    /// <summary>
    /// אירועים החופפים לטווח המבוקש — לא רק כאלה שמתחילים בתוכו, אחרת אירוע רב-יומי
    /// שהתחיל לפני תחילת החלון היה נעלם מהיומן.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<CalendarEventDto>>> List(
        [FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to)
    {
        var query = db.CalendarEvents.AsQueryable();
        if (from is { } start) query = query.Where(e => e.EndTime > start);
        if (to is { } end) query = query.Where(e => e.StartTime < end);

        var rows = await query
            .OrderBy(e => e.StartTime)
            .Select(e => new CalendarEventDto(e.Id, e.Title, e.StartTime, e.EndTime, e.IsAllDay, e.Notes))
            .ToListAsync();

        return Ok(rows);
    }

    [HttpPost]
    public async Task<ActionResult<CalendarEventDto>> Create(CreateCalendarEventRequest request)
    {
        if (request.EndTime <= request.StartTime)
            return BadRequest(new { message = "שעת הסיום חייבת להיות אחרי שעת ההתחלה." });

        var calendarEvent = new CalendarEvent
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            Title = request.Title.Trim(),
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            IsAllDay = request.IsAllDay,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.CalendarEvents.Add(calendarEvent);
        await db.SaveChangesAsync();

        return Ok(ToDto(calendarEvent));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<CalendarEventDto>> Update(Guid id, UpdateCalendarEventRequest request)
    {
        if (request.EndTime <= request.StartTime)
            return BadRequest(new { message = "שעת הסיום חייבת להיות אחרי שעת ההתחלה." });

        var calendarEvent = await db.CalendarEvents.FirstOrDefaultAsync(e => e.Id == id);
        if (calendarEvent is null) return NotFound();

        calendarEvent.Title = request.Title.Trim();
        calendarEvent.StartTime = request.StartTime;
        calendarEvent.EndTime = request.EndTime;
        calendarEvent.IsAllDay = request.IsAllDay;
        calendarEvent.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
        await db.SaveChangesAsync();

        return Ok(ToDto(calendarEvent));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var calendarEvent = await db.CalendarEvents.FirstOrDefaultAsync(e => e.Id == id);
        if (calendarEvent is null) return NotFound();

        db.CalendarEvents.Remove(calendarEvent);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private static CalendarEventDto ToDto(CalendarEvent e) =>
        new(e.Id, e.Title, e.StartTime, e.EndTime, e.IsAllDay, e.Notes);
}
