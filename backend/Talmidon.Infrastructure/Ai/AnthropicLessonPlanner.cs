using Anthropic;
using Anthropic.Models.Messages;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Talmidon.Infrastructure.Ai;

/// <summary>
/// בונה מערך שיעור עם Claude. מודל בתשלום ואיכותי — הספק ל"כשנרצה יותר טוב".
///
/// מפתח ה-API נשאר בשרת ולעולם לא מגיע לדפדפן.
///
/// הפלט הוא טקסט חופשי ולא JSON: מערך שיעור נקרא בעיניים, והמורה מעתיקה אותו. סכימה
/// נוקשה כאן הייתה מוסיפה נקודת כשל בלי להוסיף ערך.
/// </summary>
public class AnthropicLessonPlanner : ILessonPlanner
{
    private const int MaxOutputTokens = 8000;

    private readonly string? _apiKey;
    private readonly string _model;
    private readonly ILogger<AnthropicLessonPlanner> _logger;

    public AnthropicLessonPlanner(IConfiguration configuration, ILogger<AnthropicLessonPlanner> logger)
    {
        _apiKey = configuration["Anthropic:ApiKey"]
            ?? Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        _model = configuration["Ai:Anthropic:Model"] ?? "claude-opus-5";
        _logger = logger;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_apiKey);
    public string ProviderName => "Claude";

    public async Task<LessonPlanResult> BuildAsync(LessonPlanRequest request)
    {
        if (!IsConfigured)
            return new LessonPlanResult(false, null, "התכונה אינה מוגדרת בשרת.");

        var client = new AnthropicClient { ApiKey = _apiKey };

        try
        {
            var response = await client.Messages.Create(new MessageCreateParams
            {
                Model = _model,
                MaxTokens = MaxOutputTokens,
                // חשיבה אדפטיבית: בניית מערך היא משימה שדורשת תכנון, לא ניסוח בלבד
                Thinking = new ThinkingConfigAdaptive(),
                // משימה מוגדרת היטב; מאמץ בינוני מספיק, וכל בקשה עולה כסף
                OutputConfig = new OutputConfig { Effort = Effort.Medium },
                System = LessonPlanPrompt.System,
                Messages = [new() { Role = Role.User, Content = LessonPlanPrompt.Details(request) }]
            });

            if (response.StopReason == "refusal")
            {
                _logger.LogInformation("Lesson plan request was declined by the model.");
                return new LessonPlanResult(false, null, "הבקשה נדחתה. נסי לנסח את הנושא אחרת.");
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
            _logger.LogError(ex, "Lesson plan generation failed (Claude).");
            return new LessonPlanResult(false, null, "בניית המערך נכשלה. נסי שוב בעוד רגע.");
        }
    }
}
