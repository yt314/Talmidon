using System.ComponentModel.DataAnnotations;
using Talmidon.Domain.Enums;

namespace Talmidon.Api.Contracts;

public record CreateLessonRequest(
    [Required] Guid StudentId,
    [Required] DateTimeOffset StartTime,
    [Required] DateTimeOffset EndTime,
    [MaxLength(1000)] string? Reason);

public record UpdateLessonRequest(
    [Required] DateTimeOffset StartTime,
    [Required] DateTimeOffset EndTime);

public record CompleteLessonRequest(
    bool Completed,
    bool PaymentRequired,
    [Range(0, double.MaxValue)] decimal Amount,
    [MaxLength(2000)] string? Homework,
    [MaxLength(4000)] string? NoteContent,
    bool NoteVisibleToStudent,
    bool NoteVisibleToParent);

public record LessonDto(
    Guid Id,
    Guid StudentId,
    string StudentName,
    DateTimeOffset StartTime,
    DateTimeOffset EndTime,
    LessonStatus Status,
    LessonOrigin Origin,
    string? Homework,
    bool PaymentRequired,
    decimal Amount,
    bool IsPaid,
    DateTimeOffset? CompletedAt);

/// <summary>תצוגת תלמיד — ללא שדות תשלום (הרשאה: תלמיד אינו רואה סטטוס תשלומים).</summary>
public record StudentLessonDto(
    Guid Id,
    DateTimeOffset StartTime,
    DateTimeOffset EndTime,
    LessonStatus Status,
    string? Homework);

public record CreateChangeRequestRequest(
    [Required] ChangeRequestType Type,
    DateTimeOffset? ProposedStartTime,
    DateTimeOffset? ProposedEndTime,
    [MaxLength(1000)] string? Reason);

public record ChangeRequestDto(
    Guid Id,
    Guid LessonId,
    Guid StudentId,
    string StudentName,
    string ParentName,
    ChangeRequestType Type,
    DateTimeOffset LessonStartTime,
    DateTimeOffset LessonEndTime,
    DateTimeOffset? ProposedStartTime,
    DateTimeOffset? ProposedEndTime,
    string? Reason,
    ChangeRequestStatus Status,
    DateTimeOffset CreatedAt);
