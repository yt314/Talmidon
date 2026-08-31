using System.ComponentModel.DataAnnotations;
using Talmidon.Domain.Enums;

namespace Talmidon.Api.Contracts;

public record CreateStudentRequest(
    [Required, MaxLength(200)] string FullName,
    Gender? Gender,
    [MaxLength(50)] string? GradeLevel,
    DateOnly? BirthDate,
    [MaxLength(4000)] string? GeneralInfo,
    [Range(0, double.MaxValue)] decimal? DefaultPricePerLesson,
    [Range(1, 1440)] int? DefaultDurationMinutes,
    [EmailAddress, MaxLength(256)] string? LoginEmail,
    List<Guid>? ParentIds);

public record UpdateStudentRequest(
    [Required, MaxLength(200)] string FullName,
    Gender? Gender,
    [MaxLength(50)] string? GradeLevel,
    DateOnly? BirthDate,
    [MaxLength(4000)] string? GeneralInfo,
    [Range(0, double.MaxValue)] decimal? DefaultPricePerLesson,
    [Range(1, 1440)] int? DefaultDurationMinutes,
    bool IsActive);

public record StudentListItemDto(
    Guid Id,
    string FullName,
    string? GradeLevel,
    bool IsActive,
    bool HasLogin,
    int ParentCount,
    decimal? DefaultPricePerLesson,
    int? DefaultDurationMinutes);

public record StudentDetailDto(
    Guid Id,
    string FullName,
    Gender? Gender,
    string? GradeLevel,
    DateOnly? BirthDate,
    string? GeneralInfo,
    decimal? DefaultPricePerLesson,
    int? DefaultDurationMinutes,
    bool IsActive,
    bool HasLogin,
    List<ParentSummaryDto> Parents);

public record ParentSummaryDto(
    Guid Id,
    string FullName,
    string Email,
    string? Phone);

/// <summary>תצוגת הורה — ילד מקושר, לבחירה בבקשות שיעור (R2) וכד'.</summary>
public record MyChildDto(Guid Id, string FullName);

/// <summary>תצוגת תלמיד על עצמו — לפנייה מותאמת בממשק (S-self).</summary>
public record MyStudentProfileDto(string FullName, Gender? Gender);
