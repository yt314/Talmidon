using Talmidon.Domain.Common;

namespace Talmidon.Domain.Entities;

/// <summary>
/// אירוע ביומן שאינו שיעור — פגישה, חתונה, יום שהמורה חסומה בו.
///
/// ישות נפרדת ולא <see cref="Lesson"/> בלי תלמיד: שיעור נושא תלמיד, מחיר, תשלום, הערות
/// ובקשות שינוי, והוא נספר בדוחות ובחיובים. אירוע אישי שהיה מתחזה לשיעור היה נכנס לכל
/// אלה — ומופיע להורה בפורטל.
///
/// אינו חוסם קביעת שיעור באותה שעה: גם לשיעורים עצמם אין היום בדיקת חפיפה, והוספת בדיקה
/// לאירועים בלבד הייתה יוצרת התנהגות לא עקבית.
/// </summary>
public class CalendarEvent : ITenantScoped
{
    public Guid Id { get; set; }

    /// <summary>המורה שהאירוע שייך לה (= TenantId).</summary>
    public Guid TenantId { get; set; }

    public string Title { get; set; } = default!;

    public DateTimeOffset StartTime { get; set; }

    /// <summary>
    /// באירוע של יום שלם זהו חצות של היום שאחרי האחרון — מוסכמת "סוף בלעדי" שבה משתמש
    /// גם לוח השנה בצד הלקוח, כך שאירוע של יום אחד נמשך בדיוק 24 שעות.
    /// </summary>
    public DateTimeOffset EndTime { get; set; }

    public bool IsAllDay { get; set; }

    public string? Notes { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    // ניווט
    public Teacher Teacher { get; set; } = default!;
}
