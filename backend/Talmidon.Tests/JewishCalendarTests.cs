using Talmidon.Domain.Common;

namespace Talmidon.Tests;

/// <summary>
/// התאריכים הלועזיים כאן הופקו מלוח השנה העברי של ICU (‎Intl‎ עם ‎ca-hebrew‎) ולא מהמימוש
/// הנבדק — אחרת הבדיקה הייתה מאשרת את עצמה. הם משתרעים על ארבע שנים כדי לכלול גם שנה
/// מעוברת (5787), שבה פורים חל באדר ב' וכל החודשים שאחרי שבט נדחפים במקום אחד.
/// </summary>
public class JewishCalendarTests
{
    public static TheoryData<string, string> KnownHolidays => new()
    {
        // שנה פשוטה (5786)
        { "2026-03-03", "פורים" },
        { "2026-04-02", "פסח" },
        { "2026-04-08", "שביעי של פסח" },
        { "2026-05-22", "שבועות" },
        { "2026-07-23", "תשעה באב" },
        { "2026-09-12", "ראש השנה" },
        { "2026-09-21", "יום כיפור" },
        { "2026-09-26", "סוכות" },
        { "2026-10-03", "שמחת תורה" },
        // שנה מעוברת (5787) — פורים באדר ב'
        { "2027-03-23", "פורים" },
        { "2027-04-22", "פסח" },
        { "2027-06-11", "שבועות" },
        { "2027-08-12", "תשעה באב" },
        { "2027-10-11", "יום כיפור" },
        // שנים נוספות
        { "2028-03-12", "פורים" },
        { "2028-04-11", "פסח" },
        { "2029-03-01", "פורים" },
        { "2029-03-31", "פסח" }
    };

    [Theory]
    [MemberData(nameof(KnownHolidays))]
    public void NoLessonReason_NamesTheHoliday(string isoDate, string expected)
    {
        Assert.Equal(expected, JewishCalendar.NoLessonReason(DateOnly.Parse(isoDate)));
    }

    [Theory]
    [InlineData("2026-03-04", "שושן פורים")]
    [InlineData("2026-04-03", "חול המועד פסח")]
    [InlineData("2026-09-20", "ערב יום כיפור")]
    [InlineData("2026-04-01", "ערב פסח")]
    public void NoLessonReason_CoversCholHamoedAndErevChag(string isoDate, string expected)
    {
        Assert.Equal(expected, JewishCalendar.NoLessonReason(DateOnly.Parse(isoDate)));
    }

    [Theory]
    [InlineData("2026-03-10")]
    [InlineData("2026-06-15")]
    [InlineData("2026-11-04")]
    [InlineData("2027-01-20")]
    public void NoLessonReason_OrdinaryWeekdayIsNull(string isoDate)
    {
        Assert.Null(JewishCalendar.NoLessonReason(DateOnly.Parse(isoDate)));
    }

    /// <summary>
    /// פורים חל תמיד שלושים יום לפני פסח. זו האינווריאנטה שתופסת את הטעות הקלאסית —
    /// מימוש שלוקח את אדר א' בשנה מעוברת יחטיא בחודש שלם, וכאן זה ייראה מיד.
    /// </summary>
    [Fact]
    public void PurimIsAlwaysThirtyDaysBeforePesach()
    {
        for (var year = 2026; year <= 2040; year++)
        {
            var purim = SingleDayWith("פורים", year);
            var pesach = SingleDayWith("פסח", year);
            Assert.Equal(30, pesach.DayNumber - purim.DayNumber);
        }
    }

    [Fact]
    public void YomKippurIsAlwaysNineDaysAfterRoshHashanah()
    {
        for (var year = 2026; year <= 2040; year++)
        {
            var roshHashanah = FirstDayWith("ראש השנה", year);
            var yomKippur = SingleDayWith("יום כיפור", year);
            Assert.Equal(9, yomKippur.DayNumber - roshHashanah.DayNumber);
        }
    }

    /// <summary>הצום נדחה כשט' באב חל בשבת — ובכל מקרה אינו מסומן ליום שבת.</summary>
    [Fact]
    public void TishaBeAvNeverFallsOnShabbat()
    {
        for (var year = 2026; year <= 2040; year++)
        {
            var fast = AllDaysIn(year).Where(d => JewishCalendar.NoLessonReason(d)!.StartsWith("תשעה באב")).ToList();
            Assert.Single(fast);
            Assert.NotEqual(DayOfWeek.Saturday, fast[0].DayOfWeek);
        }
    }

    /// <summary>
    /// מספר ימי הדילוג בשנה לועזית יוצא 27 בכל שנה בטווח שנבדק מול ICU. המספר המדויק הוא
    /// למעשה תיעוד של מערכת הכללים: אם מישהו יוסיף או יסיר חג, הבדיקה הזו תדליק נורה.
    /// </summary>
    [Fact]
    public void NoLessonDaysBetween_CountsTwentySevenDaysAYear()
    {
        for (var year = 2026; year <= 2040; year++)
        {
            var days = JewishCalendar.NoLessonDaysBetween(new DateOnly(year, 1, 1), new DateOnly(year, 12, 31));
            Assert.Equal(27, days.Count);
        }
    }

    private static IEnumerable<DateOnly> AllDaysIn(int year)
    {
        for (var d = new DateOnly(year, 1, 1); d.Year == year; d = d.AddDays(1))
            if (JewishCalendar.NoLessonReason(d) is not null)
                yield return d;
    }

    private static DateOnly SingleDayWith(string reason, int year) =>
        Assert.Single(AllDaysIn(year), d => JewishCalendar.NoLessonReason(d) == reason);

    private static DateOnly FirstDayWith(string reason, int year) =>
        AllDaysIn(year).First(d => JewishCalendar.NoLessonReason(d) == reason);
}
