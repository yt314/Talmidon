using Talmidon.Domain.Common;

namespace Talmidon.Domain.Entities;

/// <summary>
/// הערה פדגוגית / מעקב התקדמות. שני מתגי נראוּת קובעים מי רואה אותה — וקובעים
/// אותם דרך <see cref="SetVisibility"/>, שאוכפת: נראה לתלמיד → נראה גם להורה.
/// </summary>
public class Note : ITenantScoped
{
    public Guid Id { get; set; }

    /// <summary>המורה הבעלים (= TenantId).</summary>
    public Guid TenantId { get; set; }

    public Guid StudentId { get; set; }

    /// <summary>הערה יכולה להיקשר לשיעור (אופציונלי).</summary>
    public Guid? LessonId { get; set; }

    public string Content { get; set; } = default!;

    public bool VisibleToStudent { get; set; }
    public bool VisibleToParent { get; set; }

    /// <summary>
    /// הערה שגלויה לתלמיד/ה גלויה תמיד גם להורה: אין להסתיר מהורה מה שנכתב על ילדו,
    /// והורה לא אמור להיות מופתע מהערה שהילד/ה רואה והוא לא.
    /// </summary>
    public void SetVisibility(bool toStudent, bool toParent)
    {
        VisibleToStudent = toStudent;
        VisibleToParent = toParent || toStudent;
    }

    public DateTimeOffset CreatedAt { get; set; }

    // ניווט
    public Teacher Teacher { get; set; } = default!;
    public Student Student { get; set; } = default!;
    public Lesson? Lesson { get; set; }
}
