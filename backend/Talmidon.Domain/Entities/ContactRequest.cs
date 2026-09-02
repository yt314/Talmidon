using Talmidon.Domain.Common;
using Talmidon.Domain.Enums;

namespace Talmidon.Domain.Entities;

/// <summary>
/// פנייה מהספרייה הציבורית: הורה שראה כרטיס מורה והשאיר פרטים.
///
/// נוצרת ע"י מבקר אנונימי אך שייכת לדייר (המורה שאליה פנו), כדי שהמסנן הגלובלי
/// יבטיח שמורה תראה רק את הפניות שלה. לפני זה הספרייה הסתיימה במספר טלפון,
/// ולא הייתה שום דרך לדעת כמה פניות הגיעו ומה עלה בגורלן.
/// </summary>
public class ContactRequest : ITenantScoped
{
    public Guid Id { get; set; }

    /// <summary>המורה שאליה פנו (= TenantId).</summary>
    public Guid TenantId { get; set; }

    public string FullName { get; set; } = default!;
    public string Phone { get; set; } = default!;
    public string? Email { get; set; }

    /// <summary>התחום שעליו נשאלה השאלה, כפי שהפונה בחר מרשימת התחומים של המורה.</summary>
    public string? Subject { get; set; }

    public string Message { get; set; } = default!;

    public ContactRequestStatus Status { get; set; } = ContactRequestStatus.New;

    public DateTimeOffset CreatedAt { get; set; }

    // ניווט
    public Teacher Teacher { get; set; } = default!;
}
