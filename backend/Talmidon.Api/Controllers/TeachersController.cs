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
/// פרופיל המורה עצמה: דף הכללים + הגדרות שמוצגות בספרייה הציבורית (T9).
/// Teacher ו-TeacherSubject אינם מסוננים ב-Global Query Filter (הם הדייר עצמו / מידע ציבורי),
/// לכן כל שאילתה כאן מסננת במפורש לפי TenantId (= Teacher.Id) של המורה המחוברת.
/// </summary>
[ApiController]
[Authorize(Roles = Roles.Teacher)]
[Route("api/teachers")]
public class TeachersController(TalmidonDbContext db, ICurrentTenant currentTenant) : ControllerBase
{
    private Guid TenantId => currentTenant.TenantId
        ?? throw new InvalidOperationException("No tenant in the current context.");

    [HttpGet("me")]
    public async Task<ActionResult<TeacherProfileDto>> GetMyProfile()
    {
        var profile = await db.Teachers
            .Where(t => t.Id == TenantId)
            .Select(t => new TeacherProfileDto(
                t.Id, t.FullName, t.Phone, t.Bio, t.DefaultPricePerLesson, t.RulesText, t.ContactInfo, t.IsPublic,
                t.Subjects.Select(s => new SubjectDto(s.Id, s.Name)).ToList()))
            .FirstOrDefaultAsync();
        return profile is null ? NotFound() : Ok(profile);
    }

    [HttpPut("me")]
    public async Task<IActionResult> UpdateMyProfile(UpdateTeacherProfileRequest request)
    {
        var teacher = await db.Teachers.FirstOrDefaultAsync(t => t.Id == TenantId);
        if (teacher is null) return NotFound();

        teacher.Phone = request.Phone;
        teacher.Bio = request.Bio;
        teacher.DefaultPricePerLesson = request.DefaultPricePerLesson;
        teacher.RulesText = request.RulesText;
        teacher.ContactInfo = request.ContactInfo;
        teacher.IsPublic = request.IsPublic;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("me/subjects")]
    public async Task<ActionResult<SubjectDto>> AddSubject(AddSubjectRequest request)
    {
        var name = request.Name.Trim();
        if (await db.TeacherSubjects.AnyAsync(s => s.TeacherId == TenantId && s.Name == name))
            return Conflict(new { message = "התחום כבר קיים." });

        var subject = new TeacherSubject { Id = Guid.NewGuid(), TeacherId = TenantId, Name = name };
        db.TeacherSubjects.Add(subject);
        await db.SaveChangesAsync();
        return Ok(new SubjectDto(subject.Id, subject.Name));
    }

    [HttpDelete("me/subjects/{id:guid}")]
    public async Task<IActionResult> DeleteSubject(Guid id)
    {
        var subject = await db.TeacherSubjects.FirstOrDefaultAsync(s => s.Id == id && s.TeacherId == TenantId);
        if (subject is null) return NotFound();

        db.TeacherSubjects.Remove(subject);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
