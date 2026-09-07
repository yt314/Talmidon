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
