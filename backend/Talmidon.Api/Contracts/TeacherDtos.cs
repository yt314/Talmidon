using System.ComponentModel.DataAnnotations;

namespace Talmidon.Api.Contracts;

public record UpdateTeacherProfileRequest(
    [MaxLength(40)] string? Phone,
    [MaxLength(2000)] string? Bio,
    [Range(0, double.MaxValue)] decimal DefaultPricePerLesson,
    [Range(1, 1440)] int DefaultDurationMinutes,
    [MaxLength(4000)] string? RulesText,
    [MaxLength(1000)] string? ContactInfo,
    bool IsPublic);

/// <summary>חלון זמינות שבועי. DayOfWeek: ראשון=0 ... שבת=6. שעות בפורמט "HH:mm".</summary>
public record AvailabilityWindowDto(
    [Range(0, 6)] int DayOfWeek,
    [Required] string StartTime,
    [Required] string EndTime);

public record UpdateAvailabilityRequest(List<AvailabilityWindowDto> Windows);

public record AddSubjectRequest([Required, MaxLength(100)] string Name);

/// <summary>
/// קביעת רשימת התחומים כולה בפעולה אחת. הממשק החדש עורך את הרשימה כמכלול
/// (הוספה והסרה של תגיות לפני שמירה), ולכן עדכון-מלא מדויק יותר משרשרת
/// הוספות ומחיקות שעלולה להישאר באמצע אם אחת מהן נכשלת.
/// </summary>
public record SetSubjectsRequest(List<string> Names);

public record SubjectDto(Guid Id, string Name);

/// <summary>פרופיל מורה — תצוגת בעלים (T9). כולל שדות שאינם חלק מהספרייה הציבורית (Phone).</summary>
public record TeacherProfileDto(
    Guid Id,
    string FullName,
    string? Phone,
    string? Bio,
    decimal DefaultPricePerLesson,
    int DefaultDurationMinutes,
    string? RulesText,
    string? ContactInfo,
    bool IsPublic,
    List<SubjectDto> Subjects,
    /// <summary>
    /// חותם גרסה לתמונה (גודלה בבתים), או <c>null</c> כשאין תמונה. הלקוח בונה ממנו
    /// את הכתובת מול ה-API שלו; נתיב מוחלט מהשרת היה נפתר מול מקור הפרונטאנד.
    /// </summary>
    int? PhotoVersion,
    /// <summary>האם הפרופיל מולא במידה שמאפשרת להציג אותו בספרייה — ראו TeacherProfileRules.</summary>
    bool IsProfileComplete);

/// <summary>כרטיס תקציר בספרייה הציבורית (P1).</summary>
public record PublicTeacherSummaryDto(
    Guid Id,
    string FullName,
    string? Bio,
    decimal DefaultPricePerLesson,
    List<string> Subjects,
    int? PhotoVersion);

/// <summary>דף מורה ציבורי מלא (P2) — ללא Phone הפרטי; פרטי יצירת קשר מגיעים מ-ContactInfo.</summary>
public record PublicTeacherDetailDto(
    Guid Id,
    string FullName,
    string? Bio,
    decimal DefaultPricePerLesson,
    string? RulesText,
    string? ContactInfo,
    List<string> Subjects,
    int? PhotoVersion);
