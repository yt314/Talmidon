namespace Talmidon.Infrastructure.Ai;

/// <summary>
/// קריאת הכשלים של Gemini. שגיאות הספק חוזרות כולן באותו מבנה, וההבדל ביניהן הוא כל
/// מה שקובע אם שווה לנסות שוב, להחליף מודל, לשנות את הבקשה — או להפסיק.
/// </summary>
public static class GeminiFailure
{
    /// <summary>
    /// כשל חולף אצל הספק — עומס או תקלה זמנית — שניסיון חוזר יכול לפתור. מכסה שנגמרה
    /// (429) אינה כאן בכוונה: ניסיון חוזר רק ישרוף עוד מכסה ולא יעזור.
    /// </summary>
    public static bool IsTransient(int statusCode) => statusCode is 500 or 502 or 503 or 504;

    /// <summary>
    /// האם הכשל הוא בשם המודל, כלומר האם כדאי לשאול את הספק מה כן זמין ולנסות שוב.
    ///
    /// התשובה הרשמית על שם שאינו קיים היא 404, אבל מודל שקיים ואינו תומך ביצירת תוכן
    /// חוזר כ-400 עם ההסבר בגוף. בלי הענף השני שגיאת מודל נראית ככשל כללי, והגילוי
    /// האוטומטי — כל מה שאמור להציל את המצב הזה — אינו מופעל כלל.
    /// </summary>
    public static bool IsModelProblem(int statusCode, string? errorBody) =>
        statusCode == 404 ||
        (statusCode == 400 && errorBody is not null &&
         (errorBody.Contains("is not found", StringComparison.OrdinalIgnoreCase) ||
          errorBody.Contains("not supported", StringComparison.OrdinalIgnoreCase) ||
          errorBody.Contains("NOT_FOUND", StringComparison.Ordinal)));

    /// <summary>
    /// האם הספק דחה דווקא את בקשת כיבוי החשיבה. אנחנו מכבים חשיבה כברירת מחדל כי היא
    /// עולה עשרות שניות, אבל לא כל מודל מכיר את השדה ואצל חלקם אפס אינו ערך חוקי —
    /// ואז התיקון הוא לוותר על הכיבוי, לא להיכשל.
    /// </summary>
    public static bool IsThinkingRejected(int statusCode, string? errorBody) =>
        statusCode == 400 && errorBody is not null &&
        errorBody.Contains("thinking", StringComparison.OrdinalIgnoreCase);
}
