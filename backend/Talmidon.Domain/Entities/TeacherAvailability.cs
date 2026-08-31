using Talmidon.Domain.Common;

namespace Talmidon.Domain.Entities;

/// <summary>
/// חלון זמינות שבועי של המורה (יום בשבוע + טווח שעות). משמש להדגשת שעות העבודה ביומן
/// ולאזהרה בעת קביעת שיעור מחוץ לשעות. בבעלות מורה (דייר).
/// </summary>
public class TeacherAvailability : ITenantScoped
{
    public Guid Id { get; set; }

    /// <summary>המורה הבעלים (= TenantId).</summary>
    public Guid TenantId { get; set; }

    /// <summary>יום בשבוע (ראשון=0 ... שבת=6, תואם ל-System.DayOfWeek ול-FullCalendar).</summary>
    public DayOfWeek DayOfWeek { get; set; }

    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }

    // ניווט
    public Teacher Teacher { get; set; } = default!;
}
