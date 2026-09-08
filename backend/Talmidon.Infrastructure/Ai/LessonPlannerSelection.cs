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
    /// הגדרה מפורשת גוברת. בלעדיה נבחר לפי המפתח שקיים, כשהחינמי קודם — אחרת מפתח
    /// בתשלום שנשאר מוגדר "לניסיון" היה מתחיל לחייב בשקט.
    /// </summary>
    public static LessonPlannerProvider Choose(string? geminiKey, string? anthropicKey, string? configuredProvider) =>
        configuredProvider?.Trim().ToLowerInvariant() switch
        {
            "gemini" => LessonPlannerProvider.Gemini,
            "anthropic" or "claude" => LessonPlannerProvider.Anthropic,
            "none" => LessonPlannerProvider.None,
            _ when !string.IsNullOrWhiteSpace(geminiKey) => LessonPlannerProvider.Gemini,
            _ when !string.IsNullOrWhiteSpace(anthropicKey) => LessonPlannerProvider.Anthropic,
            _ => LessonPlannerProvider.None
        };
}
