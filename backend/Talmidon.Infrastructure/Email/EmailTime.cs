using Talmidon.Domain.Common;

namespace Talmidon.Infrastructure.Email;

/// <summary>
/// תאריכים ושעות כפי שהם נכתבים במייל.
///
/// ב-DB נשמר UTC, ו-<c>DateTimeOffset.ToString</c> כותב את השעה לפי ה-offset שבערך
/// עצמו — כלומר UTC. במסכים אין בעיה, כי ‎IsraelDatePipe‎ ממיר; במייל לא היה מי
/// שימיר, ושיעור ב-17:00 נכתב "בשעה 14:00".
///
/// כל מקום שכותב שעה במייל עובר דרך כאן, כדי שלא ייווצר שוב ניסוח שמדלג על ההמרה.
/// </summary>
public static class EmailTime
{
    public static string Date(DateTimeOffset value) => AppTimeZone.ToLocal(value).ToString("dd/MM/yyyy");

    public static string Time(DateTimeOffset value) => AppTimeZone.ToLocal(value).ToString("HH:mm");

    /// <summary>"01/10/2026 17:00" — לשורה קצרה בתוך משפט.</summary>
    public static string DateAndTime(DateTimeOffset value) => $"{Date(value)} {Time(value)}";

    /// <summary>"01/10/2026 בשעה 17:00" — לפריטים ברשימה בגוף המייל.</summary>
    public static string LongDateAndTime(DateTimeOffset value) => $"{Date(value)} בשעה {Time(value)}";
}
