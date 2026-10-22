using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Talmidon.Api.Contracts;
using Talmidon.Domain.Entities;
using Talmidon.Infrastructure.Data;
using Talmidon.Infrastructure.Multitenancy;

namespace Talmidon.Api.Services;

/// <summary>
/// מעקב פדגוגי: ניהול מלא למורה (T8), עיון להורה בהערות המשותפות (R4), עיון לתלמיד
/// בהערות שסומנו גלויות לו בלבד (S3). מי רואה מה נקבע ב-<see cref="Note.SetVisibility"/>.
/// </summary>
public class NoteService(TalmidonDbContext db, ICurrentTenant currentTenant, FamilyMembers family)
{
    private static readonly Expression<Func<Note, NoteDto>> ToDto = n => new NoteDto(
        n.Id, n.StudentId, n.Student.FullName, n.LessonId, n.Content, n.VisibleToStudent, n.VisibleToParent, n.CreatedAt);

    private Guid TenantId => currentTenant.TenantId
        ?? throw new InvalidOperationException("No tenant in the current context.");

    // ===== מורה (T8) =====

    public Task<List<NoteDto>> ListAsync(Guid? studentId)
    {
        var query = db.Notes.AsQueryable();
        if (studentId is not null) query = query.Where(n => n.StudentId == studentId);
        return query.OrderByDescending(n => n.CreatedAt).Select(ToDto).ToListAsync();
    }

    public Task<NoteDto?> GetAsync(Guid id) => db.Notes.Where(n => n.Id == id).Select(ToDto).FirstOrDefaultAsync();

    public async Task<ServiceResult<NoteDto>> CreateAsync(CreateNoteRequest request)
    {
        var student = await db.Students.FirstOrDefaultAsync(s => s.Id == request.StudentId);
        if (student is null) return ServiceError.NotFound("תלמיד לא נמצא.");

        if (request.LessonId is Guid lessonId &&
            !await db.Lessons.AnyAsync(l => l.Id == lessonId && l.StudentId == request.StudentId))
            return ServiceError.Invalid("השיעור המקושר אינו תואם לתלמיד.");

        var note = new Note
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            StudentId = student.Id,
            LessonId = request.LessonId,
            Content = request.Content,
            CreatedAt = DateTimeOffset.UtcNow
        };
        note.SetVisibility(request.VisibleToStudent, request.VisibleToParent);
        db.Notes.Add(note);
        await db.SaveChangesAsync();

        return new NoteDto(note.Id, student.Id, student.FullName, note.LessonId, note.Content,
            note.VisibleToStudent, note.VisibleToParent, note.CreatedAt);
    }

    public async Task<ServiceError?> UpdateAsync(Guid id, UpdateNoteRequest request)
    {
        var note = await db.Notes.FirstOrDefaultAsync(n => n.Id == id);
        if (note is null) return ServiceError.NotFound();

        note.Content = request.Content;
        note.SetVisibility(request.VisibleToStudent, request.VisibleToParent);
        await db.SaveChangesAsync();
        return null;
    }

    public async Task<ServiceError?> DeleteAsync(Guid id)
    {
        var note = await db.Notes.FirstOrDefaultAsync(n => n.Id == id);
        if (note is null) return ServiceError.NotFound();

        db.Notes.Remove(note);
        await db.SaveChangesAsync();
        return null;
    }

    // ===== הורה (R4) ותלמיד (S3) =====

    public async Task<ServiceResult<List<ParentNoteDto>>> ForParentAsync(string userId, Guid? studentId)
    {
        var parent = await family.ParentAsync(userId);
        if (parent is null) return ServiceError.Forbidden;

        var childIds = family.ChildIdsOf(parent);
        var query = db.Notes.Where(n => n.VisibleToParent && childIds.Contains(n.StudentId));
        if (studentId is not null) query = query.Where(n => n.StudentId == studentId);

        return await query
            .OrderByDescending(n => n.CreatedAt)
            .Select(n => new ParentNoteDto(n.Id, n.StudentId, n.Student.FullName, n.Content, n.CreatedAt))
            .ToListAsync();
    }

    public async Task<ServiceResult<List<StudentNoteDto>>> ForStudentAsync(string userId)
    {
        var student = await family.StudentAsync(userId);
        if (student is null) return ServiceError.Forbidden;

        return await db.Notes
            .Where(n => n.StudentId == student.Id && n.VisibleToStudent)
            .OrderByDescending(n => n.CreatedAt)
            .Select(n => new StudentNoteDto(n.Id, n.Content, n.CreatedAt))
            .ToListAsync();
    }
}
