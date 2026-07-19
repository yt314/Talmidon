using System.ComponentModel.DataAnnotations;

namespace Talmidon.Api.Contracts;

public record CreateStudentRequest(
    [Required, MaxLength(200)] string FullName,
    [MaxLength(50)] string? GradeLevel,
    DateOnly? BirthDate,
    [MaxLength(4000)] string? GeneralInfo,
    [EmailAddress, MaxLength(256)] string? LoginEmail,
    List<Guid>? ParentIds);

public record UpdateStudentRequest(
    [Required, MaxLength(200)] string FullName,
    [MaxLength(50)] string? GradeLevel,
    DateOnly? BirthDate,
    [MaxLength(4000)] string? GeneralInfo,
    bool IsActive);

public record StudentListItemDto(
    Guid Id,
    string FullName,
    string? GradeLevel,
    bool IsActive,
    bool HasLogin,
    int ParentCount);

public record StudentDetailDto(
    Guid Id,
    string FullName,
    string? GradeLevel,
    DateOnly? BirthDate,
    string? GeneralInfo,
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
