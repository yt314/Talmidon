using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Talmidon.Api.Contracts;
using Talmidon.Infrastructure.Data;

namespace Talmidon.Api.Controllers;

/// <summary>ספריית המורות הציבורית (P1/P2) — ללא התחברות, ותמיד מוגבל למורות עם IsPublic=true.</summary>
[ApiController]
[AllowAnonymous]
[Route("api/public/teachers")]
public class PublicController(TalmidonDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<PublicTeacherSummaryDto>>> List(
        [FromQuery] string? subject, [FromQuery] string? search)
    {
        var query = db.Teachers.Where(t => t.IsPublic);

        if (!string.IsNullOrWhiteSpace(subject))
            query = query.Where(t => t.Subjects.Any(s => s.Name == subject));
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(t => t.FullName.Contains(search));

        var teachers = await query
            .OrderBy(t => t.FullName)
            .Select(t => new PublicTeacherSummaryDto(
                t.Id, t.FullName, t.Bio, t.DefaultPricePerLesson,
                t.Subjects.Select(s => s.Name).ToList()))
            .ToListAsync();
        return Ok(teachers);
    }

    /// <summary>רשימת התחומים הקיימים בקרב מורות ציבוריות — לתפריט הסינון (P1).</summary>
    [HttpGet("subjects")]
    public async Task<ActionResult<IEnumerable<string>>> ListSubjects()
    {
        var subjects = await db.Teachers
            .Where(t => t.IsPublic)
            .SelectMany(t => t.Subjects.Select(s => s.Name))
            .Distinct()
            .OrderBy(s => s)
            .ToListAsync();
        return Ok(subjects);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PublicTeacherDetailDto>> GetById(Guid id)
    {
        var teacher = await db.Teachers
            .Where(t => t.Id == id && t.IsPublic)
            .Select(t => new PublicTeacherDetailDto(
                t.Id, t.FullName, t.Bio, t.DefaultPricePerLesson, t.RulesText, t.ContactInfo,
                t.Subjects.Select(s => s.Name).ToList()))
            .FirstOrDefaultAsync();
        return teacher is null ? NotFound() : Ok(teacher);
    }
}
