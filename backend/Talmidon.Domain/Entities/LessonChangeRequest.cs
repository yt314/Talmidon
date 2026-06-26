using Talmidon.Domain.Common;
using Talmidon.Domain.Enums;

namespace Talmidon.Domain.Entities;

/// <summary>
/// בקשת הורה לביטול או לשינוי מועד של שיעור קיים.
/// השינוי מוחל בפועל רק כאשר המורה מאשרת (<see cref="ChangeRequestStatus.Approved"/>).
/// </summary>
public class LessonChangeRequest : ITenantScoped
{
    public Guid Id { get; set; }

    /// <summary>המורה הבעלים (= TenantId).</summary>
    public Guid TenantId { get; set; }

    public Guid LessonId { get; set; }
    public Guid RequestedByParentId { get; set; }

    public ChangeRequestType Type { get; set; }

    /// <summary>מועד מבוקש חדש (ל-Reschedule).</summary>
    public DateTimeOffset? ProposedStartTime { get; set; }
    public DateTimeOffset? ProposedEndTime { get; set; }

    public string? Reason { get; set; }

    public ChangeRequestStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>מתי המורה הכריעה.</summary>
    public DateTimeOffset? ResolvedAt { get; set; }

    // ניווט
    public Lesson Lesson { get; set; } = default!;
    public Parent RequestedByParent { get; set; } = default!;
}
