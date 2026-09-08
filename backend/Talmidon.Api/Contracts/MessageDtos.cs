using System.ComponentModel.DataAnnotations;
using Talmidon.Domain.Enums;

namespace Talmidon.Api.Contracts;

/// <summary>הודעה בודדת בתוך שיחה.</summary>
public record MessageDto(
    Guid Id,
    MessageAuthor SenderRole,
    string Body,
    DateTimeOffset CreatedAt);

/// <summary>שורה בתיבה — כל מה שנדרש לרשימה, בלי לטעון את ההודעות עצמן.</summary>
public record ThreadSummaryDto(
    Guid Id,
    Guid StudentId,
    string StudentName,
    MessageAuthor CounterpartRole,
    string CounterpartName,
    string Subject,
    string LastMessagePreview,
    MessageAuthor LastSenderRole,
    DateTimeOffset LastMessageAt,
    bool HasUnread,
    bool IsClosed);

/// <summary>שיחה פתוחה על כל הודעותיה.</summary>
public record ThreadDetailDto(
    Guid Id,
    Guid StudentId,
    string StudentName,
    MessageAuthor CounterpartRole,
    string CounterpartName,
    string Subject,
    Guid? RelatedNoteId,
    string? RelatedNoteContent,
    bool IsClosed,
    DateTimeOffset CreatedAt,
    IReadOnlyList<MessageDto> Messages);

/// <summary>
/// נמען אפשרי לשיחה חדשה מצד המורה: תלמידה שיש לה חשבון, או הורה מקושר.
/// מגיע כרשימה שטוחה כדי שמסך הכתיבה יסתפק בבחירה אחת.
/// </summary>
public record MessageRecipientDto(
    Guid StudentId,
    string StudentName,
    MessageAuthor Role,
    Guid Id,
    string Name);

/// <summary>פתיחת שיחה בידי המורה.</summary>
public record TeacherStartThreadRequest(
    [Required] Guid StudentId,
    MessageAuthor? CounterpartRole,
    Guid? CounterpartId,
    [Required, MaxLength(200)] string Subject,
    [Required, MaxLength(4000)] string Body);

/// <summary>
/// פתיחת שיחה בידי תלמידה או הורה. תלמידה אינה שולחת מזהה — היא פונה בעניין עצמה;
/// הורה חייב לציין באיזה ילד מדובר.
/// </summary>
public record StartThreadRequest(
    Guid? StudentId,
    Guid? RelatedNoteId,
    [Required, MaxLength(200)] string Subject,
    [Required, MaxLength(4000)] string Body);

public record PostMessageRequest([Required, MaxLength(4000)] string Body);
