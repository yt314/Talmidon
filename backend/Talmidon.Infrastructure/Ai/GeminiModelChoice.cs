namespace Talmidon.Infrastructure.Ai;

/// <summary>מודל אחד כפי שהספק מדווח עליו ב-ListModels.</summary>
public readonly record struct GeminiModelInfo(string Name, IReadOnlyList<string> SupportedMethods);

/// <summary>
/// בוחר מודל מתוך מה שהמפתח באמת רשאי להשתמש בו.
///
/// שמות המודלים של Gemini מתחלפים ונגרעים בקצב מהיר, והשכבה החינמית אינה חושפת את
/// אותם מודלים לכל מפתח. שם קבוע בקוד או בהגדרה נשבר בלי התראה, והמורה נותרת מול
/// הודעה שאין לה דרך לתקן. לכן במקום לנחש שם — שואלים את הספק מה יש, ובוחרים.
/// </summary>
public static class GeminiModelChoice
{
    /// <summary>מודלים שאינם מייצרים טקסט, גם כשהם מדווחים על generateContent.</summary>
    private static readonly string[] NotForText =
        ["embedding", "aqa", "imagen", "veo", "vision", "tts", "image", "audio", "live", "computer-use", "robotics"];

    public static string? Pick(IEnumerable<GeminiModelInfo> models) => Rank(models).FirstOrDefault();

    /// <summary>
    /// כל המודלים המתאימים, מהמועדף ומטה. הרשימה ולא רק הראשון: עומס אצל הספק הוא לכל
    /// מודל בנפרד, ולכן כשאחד עמוס יש טעם לנסות את הבא בתור במקום לוותר.
    /// </summary>
    public static IReadOnlyList<string> Rank(IEnumerable<GeminiModelInfo> models) =>
        models
            .Where(m => m.SupportedMethods.Contains("generateContent"))
            .Select(m => Strip(m.Name))
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Where(name => !NotForText.Any(bad => name.Contains(bad, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(Score)
            // שובר שוויון יציב, ומעדיף גם את הגרסה המאוחרת: 2.5 לפני 2.0
            .ThenByDescending(name => name, StringComparer.Ordinal)
            .ToList();

    /// <summary>
    /// כשל חולף אצל הספק — עומס או תקלה זמנית — שניסיון חוזר יכול לפתור. מכסה שנגמרה
    /// (429) אינה כאן בכוונה: ניסיון חוזר רק ישרוף עוד מכסה ולא יעזור.
    /// </summary>
    public static bool IsTransient(int statusCode) =>
        statusCode is 500 or 502 or 503 or 504;

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

    /// <summary>"models/gemini-2.5-flash" → "gemini-2.5-flash".</summary>
    private static string Strip(string name) =>
        name.StartsWith("models/", StringComparison.OrdinalIgnoreCase) ? name["models/".Length..] : name;

    /// <summary>
    /// flash מעל pro: הוא זה שיש לו שכבה חינמית אמיתית, והמשימה כאן היא טקסט קצר
    /// ומובנה ולא חשיבה כבדה. גרסאות preview/exp נדחקות לסוף — הן נעלמות בלי הודעה.
    /// </summary>
    private static int Score(string name)
    {
        var score = 0;
        if (name.Contains("flash", StringComparison.OrdinalIgnoreCase)) score += 100;
        else if (name.Contains("pro", StringComparison.OrdinalIgnoreCase)) score += 50;

        if (name.Contains("latest", StringComparison.OrdinalIgnoreCase)) score += 10;
        if (name.Contains("lite", StringComparison.OrdinalIgnoreCase)) score -= 5;
        if (name.Contains("preview", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("-exp", StringComparison.OrdinalIgnoreCase)) score -= 20;

        return score;
    }
}

/// <summary>
/// זוכר איזה מודל באמת ענה, כדי שהגילוי יקרה פעם אחת ולא בכל בקשה.
///
/// חי כ-Singleton בעוד ה-planner עצמו נוצר מחדש בכל בקשה (typed HttpClient), ולכן
/// הזיכרון חייב לשבת כאן ולא בשדה של ה-planner.
/// </summary>
public class GeminiModelResolver
{
    private string? _model;
    private IReadOnlyList<string>? _available;

    /// <summary>המודל שכבר ענה בהצלחה, אם היה כזה.</summary>
    public string? Resolved => Volatile.Read(ref _model);

    /// <summary>המודלים שהמפתח רשאי להשתמש בהם, מדורגים. נשאל פעם אחת ולא בכל כשל.</summary>
    public IReadOnlyList<string>? Available => Volatile.Read(ref _available);

    public void Remember(string model) => Volatile.Write(ref _model, model);

    public void RememberAvailable(IReadOnlyList<string> models) => Volatile.Write(ref _available, models);
}
