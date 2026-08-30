using Talmidon.Domain.Common;
using Talmidon.Domain.Enums;

namespace Talmidon.Domain.Entities;

/// <summary>
/// כלל שיעור חוזר שבועי (למשל "כל יום שלישי 16:00–17:00"). לא מייצג שיעורים בפועל —
/// <see cref="LessonSeriesGenerator"/> (בתשתית) מייצר מתוכה שורות <see cref="Lesson"/> רגילות
/// ועצמאיות לגמרי (כל שינוי/ביטול/השלמה פועל על מופע בודד, לא על הסדרה). בבעלות מורה (דייר).
/// </summary>
public class LessonSeries : ITenantScoped
{
    public Guid Id { get; set; }

    /// <summary>המורה הבעלים (= TenantId).</summary>
    public Guid TenantId { get; set; }

    public Guid StudentId { get; set; }

    /// <summary>יום בשבוע, סכום/שעה — נגזרים מהמופע הראשון בעת יצירת הסדרה.</summary>
    public DayOfWeek DayOfWeek { get; set; }
    public TimeOnly StartTimeOfDay { get; set; }
    public int DurationMinutes { get; set; }

    /// <summary>תאריך המופע הראשון בסדרה.</summary>
    public DateOnly SeriesStartDate { get; set; }

    /// <summary>מוגדר רק במצב "עד תאריך".</summary>
    public DateOnly? EndDate { get; set; }

    /// <summary>מוגדר רק במצב "מספר שיעורים". לכל היותר אחד מ-EndDate/OccurrenceCount מוגדר.</summary>
    public int? OccurrenceCount { get; set; }

    public int OccurrencesGenerated { get; set; }

    /// <summary>סימניה: עד איזה תאריך כבר נוצרו מופעים בפועל — כדי שהג'וב היומי יידע מאיפה להמשיך.</summary>
    public DateOnly? LastGeneratedDate { get; set; }

    /// <summary>false לאחר ביטול ידני או מיצוי הסדרה (הגיעה למספר/לתאריך הסיום).</summary>
    public bool IsActive { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    // ניווט
    public Teacher Teacher { get; set; } = default!;
    public Student Student { get; set; } = default!;
    public ICollection<Lesson> Occurrences { get; set; } = new List<Lesson>();
}
