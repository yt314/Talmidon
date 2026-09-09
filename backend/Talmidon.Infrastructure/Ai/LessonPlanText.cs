using System.Text.RegularExpressions;

namespace Talmidon.Infrastructure.Ai;

/// <summary>
/// מנקה את המערך שהמודל החזיר לטקסט שאפשר ללמד ממנו.
///
/// המערך מוצג ונשמר כטקסט רגיל, ואילו מודלים כותבים בהרגל ב-Markdown וב-LaTeX. המורה
/// רואה אז ‎\frac{1}{5}‎ במקום שבר ו-‎**מטרות**‎ במקום כותרת. ההנחיה מבקשת מהמודל להימנע
/// מכך, אבל בקשה אינה ערובה — וזה המקום שבו זה מובטח, לכל הספקים.
/// </summary>
public static class LessonPlanText
{
    // \frac{1}{5} → 1/5. גם \dfrac ו-\tfrac, שהם אותו דבר בגדלים אחרים.
    private static readonly Regex Fraction =
        new(@"\\[dt]?frac\s*\{([^{}]*)\}\s*\{([^{}]*)\}", RegexOptions.Compiled);

    /// <summary>
    /// כל פקודת LaTeX שנותרה: המוכרות הופכות לסימן היומיומי המקביל, והשאר יורדות
    /// והתוכן שסביבן נשאר. פקודה שלמה ולא החלפת מחרוזות, כי ‎\le‎ הוא תחילתו של
    /// ‎\left‎ — והחלפה לפי מחרוזת הייתה הופכת אותו ל-‎≤ft‎.
    /// </summary>
    private static readonly Regex Command = new(@"\\([a-zA-Z]+)", RegexOptions.Compiled);

    private static readonly Dictionary<string, string> Symbols = new(StringComparer.Ordinal)
    {
        ["times"] = "×",
        ["cdot"] = "·",
        ["div"] = ":",
        ["pm"] = "±",
        ["le"] = "≤",
        ["leq"] = "≤",
        ["ge"] = "≥",
        ["geq"] = "≥",
        ["neq"] = "≠",
        ["approx"] = "≈"
    };

    // $...$ ו-$$...$$ עוטפים נוסחה. מוסרים את העוטף ומשאירים את תוכנו.
    private static readonly Regex MathSpan =
        new(@"\$\$?(.+?)\$\$?", RegexOptions.Compiled | RegexOptions.Singleline);

    // סולמיות של כותרת בתחילת שורה
    private static readonly Regex Heading = new(@"^[ \t]{0,3}#{1,6}[ \t]*", RegexOptions.Compiled | RegexOptions.Multiline);

    // תבליט בכוכבית בתחילת שורה → מקף, כמו שאר הרשימות
    private static readonly Regex Bullet = new(@"^([ \t]*)\*[ \t]+", RegexOptions.Compiled | RegexOptions.Multiline);

    // סימוני הדגשה של Markdown. כוכבית בודדת אינה נוגעת — היא עשויה להיות סימן כפל.
    private static readonly Regex Emphasis = new(@"\*\*|__", RegexOptions.Compiled);

    public static string Clean(string? plan)
    {
        if (string.IsNullOrWhiteSpace(plan)) return string.Empty;

        var text = plan;

        // שברים מקוננים דורשים כמה מעברים: \frac{\frac{1}{2}}{3}
        for (var pass = 0; pass < 3 && Fraction.IsMatch(text); pass++)
            text = Fraction.Replace(text, "$1/$2");

        text = Command.Replace(text, m => Symbols.TryGetValue(m.Groups[1].Value, out var plain) ? plain : "");
        text = MathSpan.Replace(text, "$1");
        text = Heading.Replace(text, "");
        text = Bullet.Replace(text, "$1- ");
        text = Emphasis.Replace(text, "");

        // רווחים שנותרו במקום מה שהוסר
        return string.Join("\n", text.Split('\n').Select(line => line.TrimEnd())).Trim();
    }
}
