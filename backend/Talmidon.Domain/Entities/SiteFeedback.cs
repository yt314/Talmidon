namespace Talmidon.Domain.Entities;

/// <summary>
/// הודעה מהאתר בתקופת ההרצה — רעיון, תקלה או הערה, מכל מבקר.
///
/// אינה שייכת לדייר (אין <c>ITenantScoped</c>): היא נוגעת למוצר עצמו ולא למורה מסוימת,
/// ונקראת רק במסך הניהול. גם מבקר שאינו מחובר יכול לשלוח, ולכן נשמר גם מה שידוע על ההקשר
/// (הדף שממנו נשלחה) — כדי שדיווח על תקלה יהיה שווה משהו בלי להתכתב חזרה.
///
/// נשמרת במסד וגם נשלחת במייל: המייל הוא מה שגורם לקרוא אותה היום, והשורה במסד היא מה
/// שמונע ממנה ללכת לאיבוד אם המייל נכשל.
/// </summary>
public class SiteFeedback
{
    public Guid Id { get; set; }

    public string Message { get; set; } = default!;

    /// <summary>דרך ליצור קשר, אם המשתמש בחר להשאיר. לא נדרש — דיווח אנונימי עדיף על שתיקה.</summary>
    public string? ContactInfo { get; set; }

    /// <summary>הדף שממנו נשלחה ההודעה, לשחזור תקלות.</summary>
    public string? PageUrl { get; set; }

    public bool IsHandled { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
