using System.Text.RegularExpressions;

namespace Talmidon.Infrastructure.Diagnostics;

/// <summary>
/// הופך חריגה לטקסט שאפשר לשמור: כל החריגות הפנימיות ועקבות המחסנית, בלי סודות, בגודל חסום.
/// </summary>
public static partial class ErrorDetail
{
    public const int MaxMessageLength = 2000;
    public const int MaxDetailLength = 4000;

    /// <summary>
    /// מעל זה כבר לא קוראים בלאו הכי, והרצת הניקוי על מחרוזת בלי גבול עולה יותר ממה שהיא שווה.
    /// חיתוך לפני הניקוי ולא אחריו היה עלול להשאיר חצי סוד גלוי, ולכן הסדר כאן הפוך.
    /// </summary>
    private const int MaxRawLength = 50_000;

    public static string Message(Exception exception) =>
        Truncate(Redact(exception.Message), MaxMessageLength);

    /// <summary>עקבת המחסנית המלאה. <c>ToString</c> כולל כבר את כל החריגות הפנימיות.</summary>
    public static string Detail(Exception exception)
    {
        var raw = exception.ToString();
        if (raw.Length > MaxRawLength) raw = raw[..MaxRawLength];
        return Truncate(Redact(raw), MaxDetailLength);
    }

    /// <summary>
    /// מחליף ערכים של סיסמאות, מפתחות וטוקנים ב-***. הודעת שגיאה של ספק חיצוני או של
    /// מנהל החיבורים מצטטת לא פעם את מה שנשלח אליו, ומסך תקלות אינו מקום לאחסן בו מפתח
    /// פעיל — גם כשרק מנהל רואה אותו.
    /// </summary>
    public static string Redact(string text)
    {
        // הסכמה קודם: "Authorization: Bearer xyz" מכיל שם-מפתח ואחריו ערך שהוא המילה
        // Bearer בלבד, כך שבסדר ההפוך הערך האמיתי היה נשאר מעבר לרווח.
        var cleaned = SchemePattern().Replace(text, "$1 ***");
        return NamedSecretPattern().Replace(cleaned, "$1$2***");
    }

    private static string Truncate(string text, int max) =>
        text.Length <= max ? text : string.Concat(text[..(max - 1)], "…");

    /// <summary>
    /// שם-מפתח ואחריו <c>=</c> או <c>:</c> — הצורה שבה סודות מופיעים במחרוזות חיבור,
    /// ב-JSON ובשאילתות. המרכאות האופציונליות הן בשביל <c>"apiKey": "..."</c>.
    /// </summary>
    [GeneratedRegex(
        @"(password|pwd|api[-_]?key|apikey|secret|access[-_]?token|refresh[-_]?token|token|authorization)([""']?\s*[=:]\s*[""']?)[^\s;,&""')\]}]+",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex NamedSecretPattern();

    /// <summary>סכמת הרשאה בכותרת HTTP — הצורה היחידה שבה סוד בא אחרי רווח ולא אחרי סימן השמה.</summary>
    [GeneratedRegex(@"\b(Bearer|Basic)\s+[^\s;,&""')\]}]+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SchemePattern();
}
