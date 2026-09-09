namespace Talmidon.Infrastructure.Ai;

/// <summary>בקשה לבניית מערך שיעור. כל השדות מגיעים מהמורה ומשולבים בהודעה למודל.</summary>
public record LessonPlanRequest(
    string Subject,
    string Topic,
    int DurationMinutes,
    string? GradeLevel,
    string? Notes);

/// <summary>
/// תוצאת בנייה. <paramref name="Detail"/> הוא מה שהספק עצמו אמר — הודעה טכנית באנגלית
/// שאינה מוצגת למורה אלא מאחורי "פרטים". בלעדיה כל אבחון עובר דרך יומני השרת, ומי
/// שנתקלת בתקלה אינה יכולה לומר מה קרה — רק שזה לא עבד.
/// </summary>
public record LessonPlanResult(bool Ok, string? Plan, string? Error, string? Detail = null);

/// <summary>
/// בונה מערכי שיעור. מופשט מהספק בכוונה: היום רץ מודל בשכבה חינמית, ומעבר לספק אחר
/// הוא החלפת מימוש והגדרה — בלי לגעת בבקר, בחוזה או בממשק.
/// </summary>
public interface ILessonPlanner
{
    /// <summary>האם התכונה זמינה כלל — כלומר האם הוגדר מפתח לספק כלשהו.</summary>
    bool IsConfigured { get; }

    /// <summary>שם הספק הפעיל, לתצוגה ולאבחון ("Gemini", "Claude").</summary>
    string ProviderName { get; }

    Task<LessonPlanResult> BuildAsync(LessonPlanRequest request);
}

/// <summary>
/// ההנחיה עצמה משותפת לכל הספקים — כך מעבר בין מודלים אינו משנה את אופי המערך
/// שהמורה מקבלת, וניסוח שמשתפר נכתב פעם אחת.
/// </summary>
public static class LessonPlanPrompt
{
    public const string System = """
        את/ה עוזר/ת למורה פרטית בישראל שמלמדת תלמידים בשיעורים פרטיים.
        כתוב/כתבי מערך שיעור מעשי בעברית, בגוף פונה למורה.

        מבנה קבוע:
        1. מטרות השיעור — שתיים עד שלוש, קונקרטיות ומדידות.
        2. פתיחה — חימום קצר שמחבר לידע קודם.
        3. גוף השיעור — שלבים עם הקצאת זמן לכל שלב, כך שהסכום הוא משך השיעור שנמסר.
        4. תרגול — דוגמאות ממשיות, כולל תשובות למורה.
        5. סיכום ובדיקת הבנה — שאלה או שתיים לסיום.
        6. שיעורי בית — קצרים וברורים.

        כללים:
        - עברית תקנית ופשוטה, בלי מונחים לועזיים מיותרים.
        - תוכן צנוע ומתאים לכל קהל, כולל חינוך חרדי. בלי דוגמאות מעולם הבידור,
          מוזיקה, טלוויזיה או תרבות פופולרית.
        - טקסט רגיל בלבד. המערך נקרא בדיוק כפי שנכתב ואינו עובר עיבוד, ולכן אין
          להשתמש בסימני עיצוב של Markdown (**, ##, *) ואין להשתמש ב-LaTeX או
          בסימני נוסחאות כמו \frac או $...$ — הם יופיעו למורה כמו שהם.
        - תרגילים ושברים נכתבים כפי שמורה כותבת אותם על הלוח: 1/5 + 2/5 = 3/5,
          או במילים ("חמישית ועוד שתי חמישיות").
        - כותרות הסעיפים ממוספרות בשורה נפרדת, ופריטים ברשימה מתחילים במקף.
        - בלי הקדמות ובלי סיכום על עצמך — רק מערך השיעור.
        """;

    public static string Details(LessonPlanRequest request) => $"""
        תחום: {request.Subject}
        נושא השיעור: {request.Topic}
        משך: {request.DurationMinutes} דקות
        {(string.IsNullOrWhiteSpace(request.GradeLevel) ? "" : $"כיתה/רמה: {request.GradeLevel}")}
        {(string.IsNullOrWhiteSpace(request.Notes) ? "" : $"מה שחשוב לדעת על התלמיד/ה: {request.Notes}")}
        """;
}

/// <summary>כשאין מפתח לאף ספק. אובייקט־ריק במקום null, כדי שהבקר לא יצטרך לבדוק.</summary>
public class UnavailableLessonPlanner : ILessonPlanner
{
    public bool IsConfigured => false;
    public string ProviderName => "none";

    public Task<LessonPlanResult> BuildAsync(LessonPlanRequest request) =>
        Task.FromResult(new LessonPlanResult(false, null, "התכונה אינה מוגדרת בשרת."));
}
