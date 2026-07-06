using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Talmidon.Api.Contracts;
using Talmidon.Domain.Entities;
using Talmidon.Infrastructure.Auth;
using Talmidon.Infrastructure.Data;
using Talmidon.Infrastructure.Identity;
using Talmidon.Infrastructure.Multitenancy;

namespace Talmidon.Api.Controllers;

[ApiController]
[Authorize(Roles = Roles.Teacher)]
[Route("api/parents")]
public class ParentsController(
    TalmidonDbContext db,
    ICurrentTenant currentTenant,
    IAccountProvisioning provisioning,
    UserManager<ApplicationUser> userManager) : ControllerBase
{
    private Guid TenantId => currentTenant.TenantId
        ?? throw new InvalidOperationException("No tenant in the current context.");

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ParentDto>>> List()
    {
        var parents = await db.Parents
            .OrderBy(p => p.FullName)
            .Select(p => new ParentDto(p.Id, p.FullName, p.Email, p.Phone, p.StudentParents.Count))
            .ToListAsync();
        return Ok(parents);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ParentDto>> GetById(Guid id)
    {
        var parent = await db.Parents
            .Where(p => p.Id == id)
            .Select(p => new ParentDto(p.Id, p.FullName, p.Email, p.Phone, p.StudentParents.Count))
            .FirstOrDefaultAsync();
        return parent is null ? NotFound() : Ok(parent);
    }

    [HttpPost]
    public async Task<ActionResult<ParentDto>> Create(CreateParentRequest request)
    {
        await using var transaction = await db.Database.BeginTransactionAsync();

        var (user, errors) = await provisioning.CreateInvitedUserAsync(request.Email, Roles.Parent, request.FullName);
        if (user is null)
        {
            await transaction.RollbackAsync();
            return BadRequest(new { errors });
        }

        var parent = new Parent
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            UserId = user.Id,
            FullName = request.FullName,
            Email = request.Email,
            Phone = request.Phone
        };
        db.Parents.Add(parent);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();

        await provisioning.SendInvitationEmailAsync(user, request.FullName);

        var dto = new ParentDto(parent.Id, parent.FullName, parent.Email, parent.Phone, 0);
        return CreatedAtAction(nameof(GetById), new { id = parent.Id }, dto);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateParentRequest request)
    {
        var parent = await db.Parents.FirstOrDefaultAsync(p => p.Id == id);
        if (parent is null) return NotFound();

        parent.FullName = request.FullName;
        parent.Phone = request.Phone;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var parent = await db.Parents.FirstOrDefaultAsync(p => p.Id == id);
        if (parent is null) return NotFound();

        var user = await userManager.FindByIdAsync(parent.UserId);

        await using var transaction = await db.Database.BeginTransactionAsync();
        try
        {
            db.Parents.Remove(parent); // מקשרי תלמיד-הורה נמחקים ב-Cascade
            await db.SaveChangesAsync();

            if (user is not null)
                await userManager.DeleteAsync(user);

            await transaction.CommitAsync();
            return NoContent();
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync();
            // למשל: קיימים תשלומים המקושרים להורה (FK Restrict)
            return Conflict(new { message = "לא ניתן למחוק הורה עם תשלומים מקושרים." });
        }
    }
}
