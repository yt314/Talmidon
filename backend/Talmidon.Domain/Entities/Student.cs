using Talmidon.Domain.Common;
using Talmidon.Domain.Enums;

namespace Talmidon.Domain.Entities;

/// <summary>כרטיס תלמיד. בבעלות מורה (דייר).</summary>
public class Student : ITenantScoped
{
    public Guid Id { get; set; }

    /// <summary>המורה הבעלים (= TenantId).</summary>
    public Guid TenantId { get; set; }

    /// <summary>קישור התחברות (אופציונלי — תלמיד צעיר עשוי לא להתחבר).</summary>
    public string? UserId { get; set; }

    public string FullName { get; set; } = default!;
    public Gender? Gender { get; set; }
    public DateOnly? BirthDate { get; set; }
    public string? GradeLevel { get; set; }
    public string? GeneralInfo { get; set; }

    /// <summary>מחיר קבוע לשיעור לתלמיד זה. <c>null</c> = השתמש בברירת המחדל של המורה.</summary>
    public decimal? DefaultPricePerLesson { get; set; }

    /// <summary>משך קבוע לשיעור (בדקות) לתלמיד זה. <c>null</c> = השתמש בברירת המחדל של המורה.</summary>
    public int? DefaultDurationMinutes { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }

    // ניווט
    public Teacher Teacher { get; set; } = default!;
    public ICollection<StudentParent> StudentParents { get; set; } = new List<StudentParent>();
    public ICollection<Lesson> Lessons { get; set; } = new List<Lesson>();
    public ICollection<LessonSeries> LessonSeries { get; set; } = new List<LessonSeries>();
    public ICollection<Note> Notes { get; set; } = new List<Note>();
}
