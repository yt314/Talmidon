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
/// מעקב פדגוגי: ניהול מלא למורה (T8), עיון להורה בהערות המשותפות (R4),
/// עיון לתלמיד בהערות שסומנו גלויות לו בלבד (S3).
/// </summary>
[ApiController]
[Route("api/notes")]
public class NotesController(TalmidonDbContext db, ICurrentTenant currentTenant) : ControllerBase
{
    private Guid TenantId => currentTenant.TenantId
        ?? throw new InvalidOperationException("No tenant in the current context.");

    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new InvalidOperationException("No user id in the current context.");

    private Task<Parent?> CurrentParentAsync() => db.Parents.FirstOrDefaultAsync(p => p.UserId == CurrentUserId);
    private Task<Student?> CurrentStudentAsync() => db.Students.FirstOrDefaultAsync(s => s.UserId == CurrentUserId);

    // ===== מורה (T8) =====

    [Authorize(Roles = Roles.Teacher)]
    [HttpGet]
    public async Task<ActionResult<IEnumerable<NoteDto>>> List([FromQuery] Guid? studentId)
    {
        var query = db.Notes.AsQueryable();
        if (studentId is not null) query = query.Where(n => n.StudentId == studentId);

        var notes = await query
            .OrderByDescending(n => n.CreatedAt)
            .Select(n => new NoteDto(
                n.Id, n.StudentId, n.Student.FullName, n.LessonId, n.Content,
                n.VisibleToStudent, n.VisibleToParent, n.CreatedAt))
            .ToListAsync();
        return Ok(notes);
    }

    [Authorize(Roles = Roles.Teacher)]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<NoteDto>> GetById(Guid id)
    {
        var note = await db.Notes
            .Where(n => n.Id == id)
            .Select(n => new NoteDto(
                n.Id, n.StudentId, n.Student.FullName, n.LessonId, n.Content,
                n.VisibleToStudent, n.VisibleToParent, n.CreatedAt))
            .FirstOrDefaultAsync();
        return note is null ? NotFound() : Ok(note);
    }

    [Authorize(Roles = Roles.Teacher)]
    [HttpPost]
    public async Task<ActionResult<NoteDto>> Create(CreateNoteRequest request)
    {
        var student = await db.Students.FirstOrDefaultAsync(s => s.Id == request.StudentId);
        if (student is null) return NotFound(new { message = "תלמיד לא נמצא." });

        if (request.LessonId is Guid lessonId &&
            !await db.Lessons.AnyAsync(l => l.Id == lessonId && l.StudentId == request.StudentId))
            return BadRequest(new { message = "השיעור המקושר אינו תואם לתלמיד." });

        var note = new Note
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            StudentId = student.Id,
            LessonId = request.LessonId,
            Content = request.Content,
            VisibleToStudent = request.VisibleToStudent,
            // ברירת מחדל שנאכפת בשרת: הערה גלויה לתלמיד תמיד גלויה גם להורה
            VisibleToParent = request.VisibleToParent || request.VisibleToStudent,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.Notes.Add(note);
        await db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = note.Id },
            new NoteDto(note.Id, student.Id, student.FullName, note.LessonId, note.Content,
                note.VisibleToStudent, note.VisibleToParent, note.CreatedAt));
    }

    [Authorize(Roles = Roles.Teacher)]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateNoteRequest request)
    {
        var note = await db.Notes.FirstOrDefaultAsync(n => n.Id == id);
        if (note is null) return NotFound();

        note.Content = request.Content;
        note.VisibleToStudent = request.VisibleToStudent;
        note.VisibleToParent = request.VisibleToParent || request.VisibleToStudent;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [Authorize(Roles = Roles.Teacher)]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var note = await db.Notes.FirstOrDefaultAsync(n => n.Id == id);
        if (note is null) return NotFound();

        db.Notes.Remove(note);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // ===== הורה (R4) =====

    [Authorize(Roles = Roles.Parent)]
    [HttpGet("mine")]
    public async Task<ActionResult<IEnumerable<ParentNoteDto>>> MyChildrensNotes([FromQuery] Guid? studentId)
    {
        var parent = await CurrentParentAsync();
        if (parent is null) return Forbid();

        var childIds = db.StudentParents.Where(sp => sp.ParentId == parent.Id).Select(sp => sp.StudentId);
        var query = db.Notes.Where(n => n.VisibleToParent && childIds.Contains(n.StudentId));
        if (studentId is not null) query = query.Where(n => n.StudentId == studentId);

        var notes = await query
            .OrderByDescending(n => n.CreatedAt)
            .Select(n => new ParentNoteDto(n.Id, n.StudentId, n.Student.FullName, n.Content, n.CreatedAt))
            .ToListAsync();
        return Ok(notes);
    }

    // ===== תלמיד (S3) =====

    [Authorize(Roles = Roles.Student)]
    [HttpGet("my-notes")]
    public async Task<ActionResult<IEnumerable<StudentNoteDto>>> MyNotes()
    {
        var student = await CurrentStudentAsync();
        if (student is null) return Forbid();

        var notes = await db.Notes
            .Where(n => n.StudentId == student.Id && n.VisibleToStudent)
            .OrderByDescending(n => n.CreatedAt)
            .Select(n => new StudentNoteDto(n.Id, n.Content, n.CreatedAt))
            .ToListAsync();
        return Ok(notes);
    }
}
