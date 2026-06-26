using Talmidon.Domain.Common;

namespace Talmidon.Domain.Entities;

/// <summary>
/// קישור N:N בין תלמיד להורה (הורה אחד → מספר ילדים; תלמיד → עד שני הורים).
/// מפתח מורכב: (StudentId, ParentId).
/// </summary>
public class StudentParent : ITenantScoped
{
    /// <summary>המורה הבעלים (= TenantId).</summary>
    public Guid TenantId { get; set; }

    public Guid StudentId { get; set; }
    public Guid ParentId { get; set; }

    public Student Student { get; set; } = default!;
    public Parent Parent { get; set; } = default!;
}
