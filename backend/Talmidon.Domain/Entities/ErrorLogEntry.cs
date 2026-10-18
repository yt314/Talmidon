namespace Talmidon.Domain.Entities;

/// <summary>
/// תקלה שהתרחשה בשרת, כפי שאפשר לקרוא אותה אחר כך.
///
/// הסיבה לקיומה: עד כה כל שגיאה נכתבה ליומן של ספק האירוח בלבד — מקום שאי אפשר
/// להגיע אליו מתוך המוצר. בפועל זה אומר שכשמשהו נשבר אצל משתמשת אמיתית, הדרך
/// היחידה לאבחן היא לנחש, לפרוס, ולראות אם זה עזר. שורה כאן הופכת את זה לקריאה אחת.
///
/// אינה שייכת לדייר (אין <c>ITenantScoped</c>) — היא נוגעת למערכת עצמה, ונקראת רק
/// במסך הניהול. <see cref="TenantId"/> נשמר כנתון בלבד, כדי לדעת אצל מי זה קרה.
/// </summary>
public class ErrorLogEntry
{
    public Guid Id { get; set; }

    /// <summary>
    /// קוד קצר שמוצג למשתמשת ברגע התקלה ("קוד תקלה: ABC-234"). זה הקישור בין מה
    /// שהיא רואה על המסך לבין השורה הזו — בלעדיו תיאור של "משהו נפל" לא מוביל לשום שורה.
    /// </summary>
    public string Reference { get; set; } = default!;

    public DateTimeOffset OccurredAt { get; set; }

    /// <summary>מאיפה הגיעה: <c>request</c> (בקשה מהדפדפן) או <c>job</c> (משימת רקע).</summary>
    public string Source { get; set; } = default!;

    /// <summary>סוג החריגה (<c>InvalidOperationException</c> וכו') — לפי זה מזהים תקלה חוזרת.</summary>
    public string Kind { get; set; } = default!;

    public string Message { get; set; } = default!;

    /// <summary>עקבת המחסנית וכל החריגות הפנימיות, מקוצצת ומנוקה ממפתחות וסיסמאות.</summary>
    public string? Detail { get; set; }

    /// <summary>הנתיב שנקרא (<c>/api/lessons</c>) או שם המשימה.</summary>
    public string? Path { get; set; }

    public string? Method { get; set; }

    public string? UserId { get; set; }

    public string? UserEmail { get; set; }

    /// <summary>המורה שבחשבונה אירעה התקלה, אם הבקשה הגיעה מחשבון מחובר.</summary>
    public Guid? TenantId { get; set; }

    public bool IsHandled { get; set; }
}
