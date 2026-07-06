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
[Route("api/students")]
public class StudentsController(
    TalmidonDbContext db,
    ICurrentTenant currentTenant,
    IAccountProvisioning provisioning,
    UserManager<ApplicationUser> userManager) : ControllerBase
{
    private Guid TenantId => currentTenant.TenantId
        ?? throw new InvalidOperationException("No tenant in the current context.");

    [HttpGet]
    public async Task<ActionResult<IEnumerable<StudentListItemDto>>> List()
    {
        var students = await db.Students
            .OrderBy(s => s.FullName)
            .Select(s => new StudentListItemDto(
                s.Id, s.FullName, s.GradeLevel, s.IsActive, s.UserId != null, s.StudentParents.Count))
            .ToListAsync();
        return Ok(students);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<StudentDetailDto>> GetById(Guid id)
    {
        var student = await db.Students
            .Where(s => s.Id == id)
            .Select(s => new StudentDetailDto(
                s.Id, s.FullName, s.GradeLevel, s.BirthDate, s.GeneralInfo, s.IsActive, s.UserId != null,
                s.StudentParents.Select(sp => new ParentSummaryDto(
                    sp.Parent.Id, sp.Parent.FullName, sp.Parent.Email, sp.Parent.Phone)).ToList()))
            .FirstOrDefaultAsync();
        return student is null ? NotFound() : Ok(student);
    }

    [HttpPost]
    public async Task<ActionResult<StudentDetailDto>> Create(CreateStudentRequest request)
    {
        await using var transaction = await db.Database.BeginTransactionAsync();

        ApplicationUser? user = null;
        if (!string.IsNullOrWhiteSpace(request.LoginEmail))
        {
            var (created, errors) = await provisioning.CreateInvitedUserAsync(
                request.LoginEmail, Roles.Student, request.FullName);
            if (created is null)
            {
                await transaction.RollbackAsync();
                return BadRequest(new { errors });
            }
            user = created;
        }

        var student = new Student
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            UserId = user?.Id,
            FullName = request.FullName,
            GradeLevel = request.GradeLevel,
            BirthDate = request.BirthDate,
            GeneralInfo = request.GeneralInfo,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.Students.Add(student);

        if (request.ParentIds is { Count: > 0 })
        {
            // רק הורים של הדייר הנוכחי (מסונן אוטומטית)
            var validParentIds = await db.Parents
                .Where(p => request.ParentIds.Contains(p.Id))
                .Select(p => p.Id)
                .ToListAsync();

            foreach (var parentId in validParentIds)
            {
                db.StudentParents.Add(new StudentParent
                {
                    TenantId = TenantId,
                    StudentId = student.Id,
                    ParentId = parentId
                });
            }
        }

        await db.SaveChangesAsync();
        await transaction.CommitAsync();

        if (user is not null)
            await provisioning.SendInvitationEmailAsync(user, request.FullName);

        return CreatedAtAction(nameof(GetById), new { id = student.Id }, await BuildDetailAsync(student.Id));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateStudentRequest request)
    {
        var student = await db.Students.FirstOrDefaultAsync(s => s.Id == id);
        if (student is null) return NotFound();

        student.FullName = request.FullName;
        student.GradeLevel = request.GradeLevel;
        student.BirthDate = request.BirthDate;
        student.GeneralInfo = request.GeneralInfo;
        student.IsActive = request.IsActive;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var student = await db.Students.FirstOrDefaultAsync(s => s.Id == id);
        if (student is null) return NotFound();

        var user = student.UserId is not null ? await userManager.FindByIdAsync(student.UserId) : null;

        await using var transaction = await db.Database.BeginTransactionAsync();
        try
        {
            db.Students.Remove(student); // שיעורים/הערות/קישורי הורה נמחקים ב-Cascade
            await db.SaveChangesAsync();

            if (user is not null)
                await userManager.DeleteAsync(user);

            await transaction.CommitAsync();
            return NoContent();
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync();
            return Conflict(new { message = "לא ניתן למחוק את התלמיד." });
        }
    }

    // ----- קישור הורה-תלמיד -----

    [HttpPost("{id:guid}/parents/{parentId:guid}")]
    public async Task<IActionResult> LinkParent(Guid id, Guid parentId)
    {
        var studentExists = await db.Students.AnyAsync(s => s.Id == id);
        var parentExists = await db.Parents.AnyAsync(p => p.Id == parentId);
        if (!studentExists || !parentExists) return NotFound();

        if (await db.StudentParents.AnyAsync(sp => sp.StudentId == id && sp.ParentId == parentId))
            return NoContent();

        db.StudentParents.Add(new StudentParent { TenantId = TenantId, StudentId = id, ParentId = parentId });
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:guid}/parents/{parentId:guid}")]
    public async Task<IActionResult> UnlinkParent(Guid id, Guid parentId)
    {
        var link = await db.StudentParents.FirstOrDefaultAsync(sp => sp.StudentId == id && sp.ParentId == parentId);
        if (link is null) return NotFound();

        db.StudentParents.Remove(link);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private async Task<StudentDetailDto?> BuildDetailAsync(Guid id) =>
        await db.Students
            .Where(s => s.Id == id)
            .Select(s => new StudentDetailDto(
                s.Id, s.FullName, s.GradeLevel, s.BirthDate, s.GeneralInfo, s.IsActive, s.UserId != null,
                s.StudentParents.Select(sp => new ParentSummaryDto(
                    sp.Parent.Id, sp.Parent.FullName, sp.Parent.Email, sp.Parent.Phone)).ToList()))
            .FirstOrDefaultAsync();
}
