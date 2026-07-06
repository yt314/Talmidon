using System.ComponentModel.DataAnnotations;

namespace Talmidon.Api.Contracts;

public record UpdateTeacherProfileRequest(
    [MaxLength(40)] string? Phone,
    [MaxLength(2000)] string? Bio,
    [Range(0, double.MaxValue)] decimal DefaultPricePerLesson,
    [MaxLength(4000)] string? RulesText,
    [MaxLength(1000)] string? ContactInfo,
    bool IsPublic);

public record AddSubjectRequest([Required, MaxLength(100)] string Name);

public record SubjectDto(Guid Id, string Name);

/// <summary>פרופיל מורה — תצוגת בעלים (T9). כולל שדות שאינם חלק מהספרייה הציבורית (Phone).</summary>
public record TeacherProfileDto(
    Guid Id,
    string FullName,
    string? Phone,
    string? Bio,
    decimal DefaultPricePerLesson,
    string? RulesText,
    string? ContactInfo,
    bool IsPublic,
    List<SubjectDto> Subjects);

/// <summary>כרטיס תקציר בספרייה הציבורית (P1).</summary>
public record PublicTeacherSummaryDto(
    Guid Id,
    string FullName,
    string? Bio,
    decimal DefaultPricePerLesson,
    List<string> Subjects);

/// <summary>דף מורה ציבורי מלא (P2) — ללא Phone הפרטי; פרטי יצירת קשר מגיעים מ-ContactInfo.</summary>
public record PublicTeacherDetailDto(
    Guid Id,
    string FullName,
    string? Bio,
    decimal DefaultPricePerLesson,
    string? RulesText,
    string? ContactInfo,
    List<string> Subjects);
