using Talmidon.Domain.Common;

namespace Talmidon.Domain.Entities;

/// <summary>
/// חומר לימוד המשויך לתלמיד — בשלב זה קישור חיצוני (Google Drive, יוטיוב, דף תרגול).
/// תשתית בלבד: המודל מוכן להרחבה עתידית (למשל העלאת קבצים בפועל). בבעלות מורה (דייר).
/// </summary>
public class StudentResource : ITenantScoped
{
    public Guid Id { get; set; }

    /// <summary>המורה הבעלים (= TenantId).</summary>
    public Guid TenantId { get; set; }

    public Guid StudentId { get; set; }

    public string Title { get; set; } = default!;

    /// <summary>קישור לחומר (URL).</summary>
    public string Url { get; set; } = default!;

    public string? Description { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    // ניווט
    public Teacher Teacher { get; set; } = default!;
    public Student Student { get; set; } = default!;
}
