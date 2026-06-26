using Talmidon.Domain.Common;
using Talmidon.Domain.Enums;

namespace Talmidon.Domain.Entities;

/// <summary>שיעור ביומן, כולל שדות החיוב ושיעורי הבית. בבעלות מורה (דייר).</summary>
public class Lesson : ITenantScoped
{
    public Guid Id { get; set; }

    /// <summary>המורה הבעלים (= TenantId).</summary>
    public Guid TenantId { get; set; }

    public Guid StudentId { get; set; }

    public DateTimeOffset StartTime { get; set; }
    public DateTimeOffset EndTime { get; set; }

    public LessonStatus Status { get; set; }

    /// <summary>מי יזם — מורה (ישר Scheduled) או הורה (נכנס כ-Requested).</summary>
    public LessonOrigin Origin { get; set; }

    /// <summary>אם נפתח ע"י הורה — מי ביקש.</summary>
    public Guid? RequestedByParentId { get; set; }

    /// <summary>שיעורי בית. ריק → לא מוצג לתלמיד.</summary>
    public string? Homework { get; set; }

    /// <summary>האם נדרש תשלום על השיעור (המורה מסמנת בסיום).</summary>
    public bool PaymentRequired { get; set; }

    /// <summary>סכום החיוב על השיעור.</summary>
    public decimal Amount { get; set; }

    /// <summary>התשלום שכיסה את השיעור. <c>null</c> = טרם שולם.</summary>
    public Guid? PaymentId { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    // ניווט
    public Teacher Teacher { get; set; } = default!;
    public Student Student { get; set; } = default!;
    public Parent? RequestedByParent { get; set; }
    public Payment? Payment { get; set; }
    public ICollection<Note> Notes { get; set; } = new List<Note>();
    public ICollection<LessonChangeRequest> ChangeRequests { get; set; } = new List<LessonChangeRequest>();
}
