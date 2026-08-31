namespace Talmidon.Domain.Common;

/// <summary>
/// אזור הזמן היחיד שבו פועלת המערכת (מורות/תלמידים בישראל בלבד — אין ריבוי אזורי זמן במוצר).
/// כל מה שנשמר ב-DB הוא UTC; המחלקה הזו ממירה בין UTC לזמן מקומי (בעיקר עבור שיעורים חוזרים,
/// שבהם צריך לשמר "יום בשבוע + שעה" מקומיים קבועים גם כששעון קיץ/חורף משנה את ה-offset).
/// </summary>
public static class AppTimeZone
{
    public static readonly TimeZoneInfo Instance = TimeZoneInfo.FindSystemTimeZoneById("Asia/Jerusalem");

    public static DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, Instance).DateTime);

    public static DateTimeOffset ToLocal(DateTimeOffset utc) => TimeZoneInfo.ConvertTime(utc, Instance);

    /// <summary>
    /// ממיר תאריך + שעת-יום מקומיים (למשל תאריך מופע + שעת ההתחלה של סדרה) למופע UTC מדויק,
    /// תוך כיבוד שעון הקיץ/חורף החל בפועל בתאריך הספציפי הזה (ולא באזור הזמן של "עכשיו").
    /// </summary>
    public static DateTimeOffset ToUtc(DateOnly localDate, TimeOnly localTimeOfDay)
    {
        var local = localDate.ToDateTime(localTimeOfDay, DateTimeKind.Unspecified);
        var offset = Instance.GetUtcOffset(local);
        return new DateTimeOffset(local, offset);
    }
}
