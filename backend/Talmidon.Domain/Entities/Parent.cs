using Talmidon.Domain.Common;

namespace Talmidon.Domain.Entities;

/// <summary>חשבון הורה — יעד התזכורות ואישורי התשלום. בבעלות מורה (דייר).</summary>
public class Parent : ITenantScoped
{
    public Guid Id { get; set; }

    /// <summary>המורה הבעלים (= TenantId).</summary>
    public Guid TenantId { get; set; }

    /// <summary>קישור לחשבון ההתחברות (AspNetUsers).</summary>
    public string UserId { get; set; } = default!;

    public string FullName { get; set; } = default!;

    /// <summary>יעד מיילים (תזכורות ואישורי תשלום).</summary>
    public string Email { get; set; } = default!;

    public string? Phone { get; set; }

    // ניווט
    public Teacher Teacher { get; set; } = default!;
    public ICollection<StudentParent> StudentParents { get; set; } = new List<StudentParent>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
}
