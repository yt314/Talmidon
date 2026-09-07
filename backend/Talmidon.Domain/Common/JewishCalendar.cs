using System.Globalization;

namespace Talmidon.Domain.Common;

/// <summary>
/// ימים שבהם לא מתקיימים שיעורים — חגים, חול המועד וערבי חג, לפי הלוח הנהוג בישראל
/// (יום טוב אחד, לא שני ימי גלויות).
///
/// נשען על <see cref="HebrewCalendar"/> של .NET, שמכיר את עיבור השנים ואת אדר א'/אדר ב'.
/// מספור החודשים שם מתחיל בתשרי, ובשנה מעוברת נדחפים כל החודשים שאחרי שבט במקום אחד קדימה —
/// ולכן ניסן, סיוון, אב ואלול מחושבים ביחס לעיבור ולא כמספרים קבועים. פורים בשנה מעוברת חל
/// באדר ב', וזה בדיוק המקום שבו מימוש נאיבי טועה בחודש שלם.
///
/// המחלקה מחזירה גם את *סיבת* הדילוג ולא רק "כן/לא", כדי שאפשר יהיה להראות למורה למה שיעור
/// מסוים לא נוצר. דילוג שקט הוא באג מבחינת המורה, גם כשהוא נכון.
/// </summary>
public static class JewishCalendar
{
    private static readonly HebrewCalendar Hebrew = new();

    /// <summary>שם החג אם אין ללמד בתאריך הזה, או <c>null</c> ליום לימודים רגיל.</summary>
    public static string? NoLessonReason(DateOnly date)
    {
        var dt = date.ToDateTime(TimeOnly.MinValue);
        // הלוח של .NET מוגדר לטווח מוגבל (1583–2239). מחוץ לו עדיף יום לימודים רגיל
        // מאשר חריגה — לוח שנה אינו סיבה להפיל ייצור שיעורים.
        if (dt < Hebrew.MinSupportedDateTime || dt > Hebrew.MaxSupportedDateTime) return null;

        var year = Hebrew.GetYear(dt);
        var leap = Hebrew.IsLeapYear(year);
        var month = Hebrew.GetMonth(dt);
        var day = Hebrew.GetDayOfMonth(dt);

        const int tishrei = 1;
        var purimMonth = leap ? 7 : 6;   // אדר, ובשנה מעוברת אדר ב'
        var nisan = leap ? 8 : 7;
        var sivan = leap ? 10 : 9;
        var av = leap ? 12 : 11;
        var elul = leap ? 13 : 12;

        if (month == tishrei)
            return day switch
            {
                1 or 2 => "ראש השנה",
                9 => "ערב יום כיפור",
                10 => "יום כיפור",
                14 => "ערב סוכות",
                15 => "סוכות",
                >= 16 and <= 20 => "חול המועד סוכות",
                21 => "הושענא רבה",
                22 => "שמחת תורה",
                _ => null
            };

        if (month == nisan)
            return day switch
            {
                14 => "ערב פסח",
                15 => "פסח",
                >= 16 and <= 20 => "חול המועד פסח",
                21 => "שביעי של פסח",
                _ => null
            };

        if (month == sivan)
            return day switch { 5 => "ערב שבועות", 6 => "שבועות", _ => null };

        if (month == purimMonth)
            return day switch { 14 => "פורים", 15 => "שושן פורים", _ => null };

        if (month == elul && day == 29) return "ערב ראש השנה";

        if (month == av)
        {
            // כשט' באב חל בשבת הצום נדחה ליום ראשון. י' באב הוא יום ראשון בדיוק במקרה הזה.
            if (day == 9 && date.DayOfWeek != DayOfWeek.Saturday) return "תשעה באב";
            if (day == 10 && date.DayOfWeek == DayOfWeek.Sunday) return "תשעה באב (נדחה)";
        }

        return null;
    }

    public static bool IsNoLessonDay(DateOnly date) => NoLessonReason(date) is not null;

    /// <summary>הימים שאין בהם לימודים בטווח נתון (כולל שני הקצוות) — לתצוגה מקדימה למורה.</summary>
    public static IReadOnlyList<(DateOnly Date, string Reason)> NoLessonDaysBetween(DateOnly from, DateOnly to)
    {
        var days = new List<(DateOnly, string)>();
        for (var d = from; d <= to; d = d.AddDays(1))
            if (NoLessonReason(d) is { } reason)
                days.Add((d, reason));
        return days;
    }
}
