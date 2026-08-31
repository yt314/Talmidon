using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Talmidon.Api.Contracts;
using Talmidon.Infrastructure.Auth;
using Talmidon.Infrastructure.Data;

namespace Talmidon.Api.Controllers;

/// <summary>מרכז ההתראות של המורה. מסונן אוטומטית לדייר הנוכחי ע"י ה-Global Query Filter.</summary>
[ApiController]
[Authorize(Roles = Roles.Teacher)]
[Route("api/notifications")]
public class NotificationsController(TalmidonDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<NotificationDto>>> List()
    {
        var items = await db.Notifications
            .OrderByDescending(n => n.CreatedAt)
            .Take(50)
            .Select(n => new NotificationDto(
                n.Id, n.Type, n.Title, n.Message, n.LinkPath, n.IsRead, n.CreatedAt))
            .ToListAsync();
        return Ok(items);
    }

    [HttpGet("unread-count")]
    public async Task<ActionResult<UnreadCountDto>> UnreadCount()
    {
        var count = await db.Notifications.CountAsync(n => !n.IsRead);
        return Ok(new UnreadCountDto(count));
    }

    [HttpPost("{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id)
    {
        var notification = await db.Notifications.FirstOrDefaultAsync(n => n.Id == id);
        if (notification is null) return NotFound();

        if (!notification.IsRead)
        {
            notification.IsRead = true;
            await db.SaveChangesAsync();
        }
        return NoContent();
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead()
    {
        await db.Notifications
            .Where(n => !n.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true));
        return NoContent();
    }
}
