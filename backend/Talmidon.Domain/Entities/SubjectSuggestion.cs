namespace Talmidon.Domain.Entities;

/// <summary>
/// התערבות של המנהל ברשימת ההצעות לתחומי לימוד.
///
/// הרשימה נבנית מקטלוג קבוע ומתחומים שמורות כבר הזינו, ולכן שגיאת הקלדה של מורה
/// אחת מוצעת מכאן והלאה לכולן. שורה כאן מוסיפה שם לרשימה או מסתירה שם ממנה.
///
/// הסתרה ולא מחיקה: התחום ממשיך להופיע בפרופיל של מי שכבר בחרה בו, ורק מפסיק
/// להיות מוצע. מחיקה הייתה משנה נתונים של מורות אחרות בלי שביקשו.
/// </summary>
public class SubjectSuggestion
{
    public Guid Id { get; set; }

    public string Name { get; set; } = default!;

    /// <summary>‎true‎ = השם לא יוצע יותר. ‎false‎ = תוספת יזומה של המנהל.</summary>
    public bool IsHidden { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
