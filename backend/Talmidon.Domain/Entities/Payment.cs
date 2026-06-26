using Talmidon.Domain.Common;

namespace Talmidon.Domain.Entities;

/// <summary>
/// אירוע תשלום / קבלה. תשלום אחד יכול לכסות מספר שיעורים
/// (קשר 1:N דרך <see cref="Lesson.PaymentId"/>). בבעלות מורה (דייר).
/// </summary>
public class Payment : ITenantScoped
{
    public Guid Id { get; set; }

    /// <summary>המורה הבעלים (= TenantId).</summary>
    public Guid TenantId { get; set; }

    /// <summary>ההורה ששילם.</summary>
    public Guid ParentId { get; set; }

    public decimal Amount { get; set; }
    public DateOnly PaidDate { get; set; }

    /// <summary>אמצעי תשלום (מזומן / העברה / ביט...). טקסט חופשי.</summary>
    public string? Method { get; set; }

    public string? Note { get; set; }

    /// <summary>מתי נשלח מייל אישור התשלום.</summary>
    public DateTimeOffset? ConfirmationSentAt { get; set; }

    // ניווט
    public Teacher Teacher { get; set; } = default!;
    public Parent Parent { get; set; } = default!;
    public ICollection<Lesson> CoveredLessons { get; set; } = new List<Lesson>();
}
