using System.ComponentModel.DataAnnotations.Schema;

namespace Talmidon.Domain.Entities;

/// <summary>
/// אסימון רענון (Refresh Token). נשמר כ-<b>גיבוב (hash)</b> בלבד — הערך הגולמי נמסר ללקוח
/// פעם אחת. מנגנון רוטציה: בכל רענון האסימון הישן נשלל ומונפק חדש (<see cref="ReplacedByTokenHash"/>).
/// אינו תלוי-דייר (חיפוש לפי hash מתבצע לפני שנקבע הקשר הדייר).
/// </summary>
public class RefreshToken
{
    public Guid Id { get; set; }

    /// <summary>בעל האסימון (AspNetUsers).</summary>
    public string UserId { get; set; } = default!;

    /// <summary>גיבוב של האסימון הגולמי (SHA-256).</summary>
    public string TokenHash { get; set; } = default!;

    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>מתי נשלל (logout / רוטציה). null = פעיל.</summary>
    public DateTimeOffset? RevokedAt { get; set; }

    /// <summary>גיבוב האסימון שהחליף אותו ברוטציה.</summary>
    public string? ReplacedByTokenHash { get; set; }

    [NotMapped]
    public bool IsActive => RevokedAt is null && DateTimeOffset.UtcNow < ExpiresAt;
}
