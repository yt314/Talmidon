using System.ComponentModel.DataAnnotations;

namespace Talmidon.Api.Contracts;

public record UpdateTeacherProfileRequest(
    [MaxLength(40)] string? Phone,
    [EmailAddress, MaxLength(256)] string? ContactEmail,
    [MaxLength(100)] string? City,
    [MaxLength(100)] string? Neighborhood,
    [MaxLength(2000)] string? Bio,
    [Range(0, double.MaxValue)] decimal DefaultPricePerLesson,
    [Range(1, 1440)] int DefaultDurationMinutes,
    [MaxLength(4000)] string? RulesText,
    bool IsPublic,
    /// <summary>
    /// אופציונלי בכוונה: לקוח שאינו שולח את השדה משאיר את הערך הקיים, במקום
    /// לאפס אותו ל-false ולהעלים בשקט מורה מהספרייה.
    /// </summary>
    bool? AcceptingStudents = null);

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
    string? ContactEmail,
    string? City,
    string? Neighborhood,
    string? Bio,
    decimal DefaultPricePerLesson,
    int DefaultDurationMinutes,
    string? RulesText,
    bool IsPublic,
    List<SubjectDto> Subjects,
    /// <summary>
    /// חותם גרסה לתמונה (גודלה בבתים), או <c>null</c> כשאין תמונה. הלקוח בונה ממנו
    /// את הכתובת מול ה-API שלו; נתיב מוחלט מהשרת היה נפתר מול מקור הפרונטאנד.
    /// </summary>
    int? PhotoVersion,
    /// <summary>האם הפרופיל מולא במידה שמאפשרת להציג אותו בספרייה — ראו TeacherProfileRules.</summary>
    bool IsProfileComplete,
    bool AcceptingStudents);

/// <summary>כרטיס תקציר בספרייה הציבורית (P1).</summary>
public record PublicTeacherSummaryDto(
    Guid Id,
    string FullName,
    string? Bio,
    string? City,
    string? Neighborhood,
    decimal DefaultPricePerLesson,
    List<string> Subjects,
    int? PhotoVersion,
    bool AcceptingStudents);

/// <summary>
/// דף מורה ציבורי מלא (P2). הטלפון והמייל כאן הם פרטי יצירת הקשר שהמורה מילאה
/// בפרופיל שלה כדי שיפורסמו — הם מוגשים רק כשהיא בספרייה, כמו שאר הכרטיס.
/// </summary>
public record PublicTeacherDetailDto(
    Guid Id,
    string FullName,
    string? Bio,
    string? City,
    string? Neighborhood,
    string? Phone,
    string? ContactEmail,
    decimal DefaultPricePerLesson,
    string? RulesText,
    List<string> Subjects,
    int? PhotoVersion,
    bool AcceptingStudents);
