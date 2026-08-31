using Talmidon.Domain.Common;

namespace Talmidon.Tests;

/// <summary>
/// בודק שהמרת זמן-מקומי-קבוע ל-UTC מכבדת שעון קיץ/חורף לפי התאריך הספציפי (לא לפי "עכשיו") —
/// זה בדיוק התיקון שמונע משיעורים חוזרים "לזוז שעה" סביב מעברי שעון (ראו LessonSeriesGenerator).
/// </summary>
public class AppTimeZoneTests
{
    [Fact]
    public void ToUtc_SameLocalHour_HasDifferentOffsetInWinterAndSummer()
    {
        // ינואר = שעון חורף בישראל (UTC+2); יולי = שעון קיץ (UTC+3) — נכון בוודאות בכל שנה,
        // בלי תלות בתאריך המדויק (המשתנה) של תחילת/סוף שעון הקיץ.
        var winter = AppTimeZone.ToUtc(new DateOnly(2026, 1, 13), new TimeOnly(16, 0));
        var summer = AppTimeZone.ToUtc(new DateOnly(2026, 7, 14), new TimeOnly(16, 0));

        Assert.Equal(TimeSpan.FromHours(2), winter.Offset);
        Assert.Equal(TimeSpan.FromHours(3), summer.Offset);
    }

    [Fact]
    public void ToUtc_PreservesTheLocalWallClockTime_RegardlessOfDst()
    {
        var winter = AppTimeZone.ToUtc(new DateOnly(2026, 1, 13), new TimeOnly(16, 0));
        var summer = AppTimeZone.ToUtc(new DateOnly(2026, 7, 14), new TimeOnly(16, 0));

        Assert.Equal(new TimeOnly(16, 0), TimeOnly.FromDateTime(winter.DateTime));
        Assert.Equal(new TimeOnly(16, 0), TimeOnly.FromDateTime(summer.DateTime));
    }
}
