using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Talmidon.Api.Contracts;
using Talmidon.Domain.Entities;
using Talmidon.Infrastructure.Auth;
using Talmidon.Infrastructure.Data;
using Talmidon.Infrastructure.Multitenancy;

namespace Talmidon.Api.Controllers;

/// <summary>
/// חומרי לימוד (קישורים) המשויכים לתלמיד — תשתית בסיסית. מסונן אוטומטית לדייר הנוכחי
/// ע"י ה-Global Query Filter. הממשק (UI) ייבנה בשלב מאוחר יותר.
/// </summary>
[ApiController]
[Authorize(Roles = Roles.Teacher)]
[Route("api/students/{studentId:guid}/resources")]
public class StudentResourcesController(TalmidonDbContext db, ICurrentTenant currentTenant) : ControllerBase
{
    private Guid TenantId => currentTenant.TenantId
        ?? throw new InvalidOperationException("No tenant in the current context.");

    [HttpGet]
    public async Task<ActionResult<IEnumerable<StudentResourceDto>>> List(Guid studentId)
    {
        var items = await db.StudentResources
            .Where(r => r.StudentId == studentId)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new StudentResourceDto(r.Id, r.StudentId, r.Title, r.Url, r.Description, r.CreatedAt))
            .ToListAsync();
        return Ok(items);
    }

    [HttpPost]
    public async Task<ActionResult<StudentResourceDto>> Create(Guid studentId, CreateStudentResourceRequest request)
    {
        if (!await db.Students.AnyAsync(s => s.Id == studentId))
            return NotFound(new { message = "תלמיד לא נמצא." });

        var resource = new StudentResource
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            StudentId = studentId,
            Title = request.Title,
            Url = request.Url,
            Description = request.Description,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.StudentResources.Add(resource);
        await db.SaveChangesAsync();

        return Ok(new StudentResourceDto(
            resource.Id, resource.StudentId, resource.Title, resource.Url, resource.Description, resource.CreatedAt));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid studentId, Guid id)
    {
        var resource = await db.StudentResources.FirstOrDefaultAsync(r => r.Id == id && r.StudentId == studentId);
        if (resource is null) return NotFound();

        db.StudentResources.Remove(resource);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
