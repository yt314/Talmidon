using System.ComponentModel.DataAnnotations;

namespace Talmidon.Api.Contracts;

public record AdminTeacherDto(
    Guid Id,
    string FullName,
    string Email,
    DateTimeOffset CreatedAt,
    bool IsPublic,
    int StudentCount,
    bool IsLockedOut);

/// <summary>
/// שורה ברשימת ההצעות בעיני המנהל. <c>IsBuiltIn</c> מסמן קטלוג קבוע שאינו ניתן
/// להסרה, ו-<c>IsInUse</c> מסמן שמורה כלשהי כבר בחרה בתחום.
/// </summary>
public record AdminSubjectSuggestionDto(string Name, bool IsHidden, bool IsBuiltIn, bool IsInUse);

public record AdminSubjectSuggestionRequest([Required, MaxLength(100)] string Name);

/// <summary>
/// שורה ברשימת התקלות. בלי עקבת המחסנית במכוון — היא נטענת רק כשפותחים תקלה אחת,
/// אחרת רשימה של מאה תקלות שוקלת כמעט מגה־בייט וטוענת אטית בדיוק כשממהרים.
/// </summary>
public record AdminErrorDto(
    Guid Id,
    string Reference,
    DateTimeOffset OccurredAt,
    string Source,
    string Kind,
    string Message,
    string? Path,
    string? Method,
    string? UserEmail,
    bool IsHandled);

/// <summary>תקלה אחת, עם עקבת המחסנית המלאה.</summary>
public record AdminErrorDetailDto(
    Guid Id,
    string Reference,
    DateTimeOffset OccurredAt,
    string Source,
    string Kind,
    string Message,
    string? Detail,
    string? Path,
    string? Method,
    string? UserEmail,
    Guid? TenantId,
    bool IsHandled);

/// <summary>מספר התקלות שעוד לא סומנו כטופלו — למספר שליד "תקלות" בתפריט הניהול.</summary>
public record AdminErrorCountDto(int Open);
