using System.ComponentModel.DataAnnotations;

namespace Talmidon.Api.Contracts;

public record CreateNoteRequest(
    [Required] Guid StudentId,
    Guid? LessonId,
    [Required, MaxLength(4000)] string Content,
    bool VisibleToStudent,
    bool VisibleToParent);

public record UpdateNoteRequest(
    [Required, MaxLength(4000)] string Content,
    bool VisibleToStudent,
    bool VisibleToParent);

public record NoteDto(
    Guid Id,
    Guid StudentId,
    string StudentName,
    Guid? LessonId,
    string Content,
    bool VisibleToStudent,
    bool VisibleToParent,
    DateTimeOffset CreatedAt);

/// <summary>תצוגת הורה — ללא מתגי הנראוּת (לא רלוונטיים לצפייה).</summary>
public record ParentNoteDto(
    Guid Id,
    Guid StudentId,
    string StudentName,
    string Content,
    DateTimeOffset CreatedAt);

/// <summary>תצוגת תלמיד — הערה בלבד, ללא זהות/הרשאות נוספות.</summary>
public record StudentNoteDto(
    Guid Id,
    string Content,
    DateTimeOffset CreatedAt);
