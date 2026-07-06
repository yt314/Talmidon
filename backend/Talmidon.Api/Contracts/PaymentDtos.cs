using System.ComponentModel.DataAnnotations;

namespace Talmidon.Api.Contracts;

/// <summary>שיעור שנדרש עליו תשלום ולא כוסה עדיין ע"י שום Payment.</summary>
public record OpenChargeDto(
    Guid LessonId,
    Guid StudentId,
    string StudentName,
    DateTimeOffset LessonStartTime,
    decimal Amount);

public record CreatePaymentRequest(
    [Required] Guid ParentId,
    [Required, MinLength(1)] List<Guid> LessonIds,
    [Required] DateOnly PaidDate,
    [MaxLength(50)] string? Method,
    [MaxLength(1000)] string? Note);

public record PaymentDto(
    Guid Id,
    Guid ParentId,
    string ParentName,
    decimal Amount,
    DateOnly PaidDate,
    string? Method,
    string? Note,
    int LessonCount,
    DateTimeOffset? ConfirmationSentAt);

public record PaymentLessonDto(
    Guid LessonId,
    Guid StudentId,
    string StudentName,
    DateTimeOffset StartTime,
    decimal Amount);

public record PaymentDetailDto(
    Guid Id,
    Guid ParentId,
    string ParentName,
    decimal Amount,
    DateOnly PaidDate,
    string? Method,
    string? Note,
    DateTimeOffset? ConfirmationSentAt,
    List<PaymentLessonDto> Lessons);
