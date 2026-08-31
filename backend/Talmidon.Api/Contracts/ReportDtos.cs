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
