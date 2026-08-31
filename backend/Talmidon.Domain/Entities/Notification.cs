using Talmidon.Domain.Common;
using Talmidon.Domain.Enums;

namespace Talmidon.Domain.Entities;

/// <summary>התראה במרכז ההתראות של המורה (בקשות מהורים וכד'). בבעלות מורה (דייר).</summary>
public class Notification : ITenantScoped
{
    public Guid Id { get; set; }

    /// <summary>המורה הבעלים (= TenantId).</summary>
    public Guid TenantId { get; set; }

    public NotificationType Type { get; set; }

    public string Title { get; set; } = default!;
    public string Message { get; set; } = default!;

    /// <summary>נתיב יעד בממשק בלחיצה על ההתראה (למשל "/app/lessons"). אופציונלי.</summary>
    public string? LinkPath { get; set; }

    public bool IsRead { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    // ניווט
    public Teacher Teacher { get; set; } = default!;
}
