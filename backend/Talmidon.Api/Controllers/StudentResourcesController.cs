using System.Security.Claims;
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
/// חומרי לימוד (קישורים) המשויכים לתלמיד: ניהול מלא למורה, וצפייה בלבד לתלמיד ולהוריו.
/// מסונן אוטומטית לדייר הנוכחי ע"י ה-Global Query Filter; בנוסף, כל נקודת קצה של פורטל
/// מצמצמת בעצמה לתלמיד המחובר או לילדיו של ההורה המחובר, כדי שגם שכחה של המסנן לא תדלוף.
///
/// חומר לימוד נועד מלכתחילה להיות משותף — בשונה מהערה פדגוגית אין לו דגלי נראות,
/// וכל חומר שהמורה מוסיפה לתלמיד גלוי לו ולהוריו.
/// </summary>
[ApiController]
[Route("api/students/{studentId:guid}/resources")]
public class StudentResourcesController(TalmidonDbContext db, ICurrentTenant currentTenant) : ControllerBase
{
    private Guid TenantId => currentTenant.TenantId
        ?? throw new InvalidOperationException("No tenant in the current context.");

    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new InvalidOperationException("No user id in the current context.");

    private Task<Parent?> CurrentParentAsync() => db.Parents.FirstOrDefaultAsync(p => p.UserId == CurrentUserId);
    private Task<Student?> CurrentStudentAsync() => db.Students.FirstOrDefaultAsync(s => s.UserId == CurrentUserId);

    // ===== מורה =====

    [Authorize(Roles = Roles.Teacher)]
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

    [Authorize(Roles = Roles.Teacher)]
    [HttpPost]
    public async Task<ActionResult<StudentResourceDto>> Create(Guid studentId, CreateStudentResourceRequest request)
    {
        if (!await db.Students.AnyAsync(s => s.Id == studentId))
            return NotFound(new { message = "תלמיד לא נמצא." });

        // ‎[Url]‎ מסנן כבר את רוב הזבל, אבל מאשר גם ftp://; כאן מצמצמים ל-http/https בלבד,
        // כי הקישור מוצג לתלמיד ולהורה כ-‎<a href>‎ לחיץ
        if (!IsSafeHttpUrl(request.Url))
            return BadRequest(new { message = "כתובת הקישור חייבת להתחיל ב-http:// או ב-https://." });

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

    [Authorize(Roles = Roles.Teacher)]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid studentId, Guid id)
    {
        var resource = await db.StudentResources.FirstOrDefaultAsync(r => r.Id == id && r.StudentId == studentId);
        if (resource is null) return NotFound();

        db.StudentResources.Remove(resource);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // ===== תלמיד =====

    /// <summary>חומרי הלימוד של התלמיד המחובר בלבד.</summary>
    [Authorize(Roles = Roles.Student)]
    [HttpGet("/api/resources/my-resources")]
    public async Task<ActionResult<IEnumerable<PortalResourceDto>>> MyResources()
    {
        var student = await CurrentStudentAsync();
        if (student is null) return Forbid();

        var items = await db.StudentResources
            .Where(r => r.StudentId == student.Id)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new PortalResourceDto(r.Id, r.StudentId, r.Student.FullName, r.Title, r.Url, r.Description, r.CreatedAt))
            .ToListAsync();
        return Ok(items);
    }

    // ===== הורה =====

    /// <summary>חומרי הלימוד של ילדי ההורה המחובר, עם סינון אופציונלי לילד מסוים.</summary>
    [Authorize(Roles = Roles.Parent)]
    [HttpGet("/api/resources/mine")]
    public async Task<ActionResult<IEnumerable<PortalResourceDto>>> MyChildrensResources([FromQuery] Guid? studentId)
    {
        var parent = await CurrentParentAsync();
        if (parent is null) return Forbid();

        var childIds = db.StudentParents.Where(sp => sp.ParentId == parent.Id).Select(sp => sp.StudentId);
        var query = db.StudentResources.Where(r => childIds.Contains(r.StudentId));
        if (studentId is not null) query = query.Where(r => r.StudentId == studentId);

        var items = await query
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new PortalResourceDto(r.Id, r.StudentId, r.Student.FullName, r.Title, r.Url, r.Description, r.CreatedAt))
            .ToListAsync();
        return Ok(items);
    }

    /// <summary>נכון רק ל-http/https — ראו ההערה ב-<see cref="Create"/>.</summary>
    private static bool IsSafeHttpUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
