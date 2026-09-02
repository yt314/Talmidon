using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Talmidon.Api.Contracts;
using Talmidon.Domain.Enums;
using Talmidon.Infrastructure.Auth;
using Talmidon.Infrastructure.Data;

namespace Talmidon.Api.Controllers;

/// <summary>
/// הפניות שהגיעו למורה מהספרייה הציבורית. הקריאה מסוננת אוטומטית לדייר הנוכחי,
/// ולכן מורה רואה רק את הפניות שלה.
/// </summary>
[ApiController]
[Authorize(Roles = Roles.Teacher)]
[Route("api/contact-requests")]
public class ContactRequestsController(TalmidonDbContext db) : ControllerBase
{
    /// <summary>הפניות, החדשות תחילה. ‎status‎ מסנן לפי מצב טיפול.</summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ContactRequestDto>>> List([FromQuery] int? status)
    {
        var query = db.ContactRequests.AsQueryable();
        if (status is not null) query = query.Where(c => (int)c.Status == status);

        var items = await query
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new ContactRequestDto(
                c.Id, c.FullName, c.Phone, c.Email, c.Subject, c.Message, (int)c.Status, c.CreatedAt))
            .ToListAsync();
        return Ok(items);
    }

    /// <summary>כמה פניות חדשות ממתינות — למחוון בלוח המחוונים ובתפריט.</summary>
    [HttpGet("new-count")]
    public async Task<ActionResult<int>> NewCount() =>
        Ok(await db.ContactRequests.CountAsync(c => c.Status == ContactRequestStatus.New));

    [HttpPut("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, UpdateContactRequestStatusRequest request)
    {
        if (!Enum.IsDefined(typeof(ContactRequestStatus), request.Status))
            return BadRequest(new { message = "מצב לא תקין." });

        var contact = await db.ContactRequests.FirstOrDefaultAsync(c => c.Id == id);
        if (contact is null) return NotFound();

        contact.Status = (ContactRequestStatus)request.Status;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var contact = await db.ContactRequests.FirstOrDefaultAsync(c => c.Id == id);
        if (contact is null) return NotFound();

        db.ContactRequests.Remove(contact);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
