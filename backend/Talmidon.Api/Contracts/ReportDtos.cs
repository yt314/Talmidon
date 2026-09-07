namespace Talmidon.Api.Contracts;

/// <summary>שורת פילוח הכנסות לפי תלמיד בחודש נתון.</summary>
public record StudentIncomeDto(
    Guid StudentId,
    string StudentName,
    int Lessons,
    decimal Charged,
    decimal Paid);

/// <summary>דוח הכנסות חודשי למורה (T) — שיעורים שהתקיימו, חיובים, שולם ופתוח.</summary>
public record IncomeReportDto(
    int Year,
    int Month,
    int CompletedLessons,
    decimal TotalCharged,
    decimal TotalPaid,
    decimal TotalOutstanding,
    List<StudentIncomeDto> ByStudent);


/// <summary>שורת נוכחות לפי תלמיד: כמה התקיימו, בוטלו ולא הגיע, וכמה שעות למדו בפועל.</summary>
public record StudentAttendanceDto(
    Guid StudentId,
    string StudentName,
    int Completed,
    int Cancelled,
    int NoShow,
    decimal Hours,
    /// <summary>
    /// שיעור הביטולים ואי-ההגעות מתוך כל השיעורים שהיו אמורים להתקיים, באחוזים.
    /// זה המספר שמראה מי נושר בשקט — ולכן הוא מחושב בשרת ולא בכל מסך מחדש.
    /// </summary>
    int MissedPercent);

/// <summary>
/// דוח נוכחות חודשי. נספרים רק שיעורים שהמועד שלהם עבר וטופלו (התקיים/בוטל/לא הגיע) —
/// שיעור שעדיין מתוזמן אינו "החמצה" ואין להעניש עליו תלמיד בסטטיסטיקה.
/// </summary>
public record AttendanceReportDto(
    int Year,
    int Month,
    int Completed,
    int Cancelled,
    int NoShow,
    decimal Hours,
    List<StudentAttendanceDto> ByStudent);
