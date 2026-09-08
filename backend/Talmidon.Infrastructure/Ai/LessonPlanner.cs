using Anthropic;
using Anthropic.Models.Messages;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Talmidon.Infrastructure.Ai;

/// <summary>בקשה לבניית מערך שיעור. כל השדות מגיעים מהמורה ומשולבים בהודעת המשתמש.</summary>
public record LessonPlanRequest(
    string Subject,
    string Topic,
    int DurationMinutes,
    string? GradeLevel,
    string? Notes);

public record LessonPlanResult(bool Ok, string? Plan, string? Error);

public interface ILessonPlanner
{
    /// <summary>האם התכונה זמינה כלל — כלומר האם הוגדר מפתח API.</summary>
    bool IsConfigured { get; }

    Task<LessonPlanResult> BuildAsync(LessonPlanRequest request);
}

/// <summary>
/// בונה מערך שיעור בעזרת Claude.
///
/// מפתח ה-API נשאר בשרת ולעולם לא מגיע לדפדפן. בלי מפתח מוגדר התכונה פשוט אינה זמינה
/// (<see cref="IsConfigured"/>), והממשק מסתיר אותה — עדיף מאשר כפתור שנכשל בלחיצה.
///
/// הפלט הוא טקסט חופשי ולא JSON: מערך שיעור נקרא בעיניים, והמורה מעתיקה אותו. סכימה
/// נוקשה כאן הייתה מוסיפה נקודת כשל בלי להוסיף ערך.
/// </summary>
public class LessonPlanner : ILessonPlanner
{
    private const string ModelId = "claude-opus-5";

    /// <summary>
    /// תקרה שמרנית: מערך שיעור סביר נכנס בהרבה פחות, והתקרה היא הגנה מפני חשבון מפתיע.
    /// כוללת גם את טוקני החשיבה, ולכן לא נמוכה מדי — תקרה הדוקה הייתה קוטעת מערך באמצע.
    /// </summary>
    private const int MaxOutputTokens = 8000;

    private readonly string? _apiKey;
    private readonly ILogger<LessonPlanner> _logger;

    public LessonPlanner(IConfiguration configuration, ILogger<LessonPlanner> logger)
    {
        _apiKey = configuration["Anthropic:ApiKey"]
            ?? Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        _logger = logger;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_apiKey);

    public async Task<LessonPlanResult> BuildAsync(LessonPlanRequest request)
    {
        if (!IsConfigured)
            return new LessonPlanResult(false, null, "התכונה אינה מוגדרת בשרת.");

        var client = new AnthropicClient { ApiKey = _apiKey };

        var system = """
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
            - בלי הקדמות ובלי סיכום על עצמך — רק מערך השיעור.
            """;

        var details = $"""
            תחום: {request.Subject}
            נושא השיעור: {request.Topic}
            משך: {request.DurationMinutes} דקות
            {(string.IsNullOrWhiteSpace(request.GradeLevel) ? "" : $"כיתה/רמה: {request.GradeLevel}")}
            {(string.IsNullOrWhiteSpace(request.Notes) ? "" : $"מה שחשוב לדעת על התלמיד/ה: {request.Notes}")}
            """;

        try
        {
            var response = await client.Messages.Create(new MessageCreateParams
            {
                Model = ModelId,
                MaxTokens = MaxOutputTokens,
                // חשיבה אדפטיבית: בניית מערך היא משימה שדורשת תכנון, לא ניסוח בלבד
                Thinking = new ThinkingConfigAdaptive(),
                // בניית מערך היא משימה מוגדרת היטב; מאמץ בינוני מספיק כאן, וכל בקשה עולה כסף
                OutputConfig = new OutputConfig { Effort = Effort.Medium },
                System = system,
                Messages = [new() { Role = Role.User, Content = details }]
            });

            if (response.StopReason == "refusal")
            {
                _logger.LogInformation("Lesson plan request was declined by the model.");
                return new LessonPlanResult(false, null,
                    "הבקשה נדחתה. נסי לנסח את הנושא אחרת.");
            }

            var plan = string.Join("\n", response.Content
                .Select(b => b.Value)
                .OfType<TextBlock>()
                .Select(t => t.Text))
                .Trim();

            return string.IsNullOrWhiteSpace(plan)
                ? new LessonPlanResult(false, null, "לא התקבל מערך שיעור. נסי שוב.")
                : new LessonPlanResult(true, plan, null);
        }
        catch (Exception ex)
        {
            // הודעת השגיאה של הספק עלולה להכיל פרטי חשבון — נרשמת בלוג ואינה מוחזרת למורה
            _logger.LogError(ex, "Lesson plan generation failed.");
            return new LessonPlanResult(false, null, "בניית המערך נכשלה. נסי שוב בעוד רגע.");
        }
    }
}
