namespace Talmidon.Infrastructure.Ai;

public enum LessonPlannerProvider
{
    None,
    Gemini,
    Anthropic
}

/// <summary>
/// בוחר איזה ספק ירשם. פונקציה נפרדת ולא לוגיקה בתוך רישום השירותים, כי זו ההחלטה
/// שמאפשרת להחליף מודל בהגדרה במקום בקוד — וכזו ראוי שתהיה בעלת שם ובדוקה.
/// </summary>
public static class LessonPlannerSelection
{
    /// <summary>
    /// התכונה כבויה עד שמבקשים אותה במפורש, ב-AI_PROVIDER.
    ///
    /// קודם היא נדלקה מעצם קיומו של מפתח בסביבה. זה נראה נוח, אבל תכונה שמדליקה את
    /// עצמה כי מפתח במקרה מונח שם היא הפתעה: היא עולה זמן וכסף, והכיבוי שלה דורש
    /// לגלות קודם שהיא בכלל פועלת. מפתח שנשאר מוגדר אינו בקשה להשתמש בו.
    ///
    /// המפתחות עדיין נבדקים — ספק שנבחר בלי מפתח אינו יכול לפעול, והמסך אומר זאת.
    /// </summary>
    public static LessonPlannerProvider Choose(string? geminiKey, string? anthropicKey, string? configuredProvider) =>
        configuredProvider?.Trim().ToLowerInvariant() switch
        {
            "gemini" => LessonPlannerProvider.Gemini,
            "anthropic" or "claude" => LessonPlannerProvider.Anthropic,
            _ => LessonPlannerProvider.None
        };
}
