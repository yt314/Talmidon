using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Talmidon.Infrastructure.Ai;

/// <summary>
/// בונה מערך שיעור עם Gemini, שיש לו שכבה חינמית — הספק ברירת המחדל היום.
///
/// נכתב מול ה-REST הישיר ולא מול SDK: הקריאה היא בקשת JSON אחת, וספרייה נוספת הייתה
/// מוסיפה תלות ומשקל בלי להרוויח דבר.
///
/// שם המודל ניתן להגדרה. שמות המודלים של Gemini מתחלפים בקצב מהיר, ולכן כשהשרת מחזיר
/// "לא נמצא" המימוש שואל את הספק אילו מודלים כן זמינים למפתח הזה ורושם אותם בלוג —
/// כך תקלה כזו נפתרת בשינוי הגדרה במקום בחקירה.
/// </summary>
public class GeminiLessonPlanner : ILessonPlanner
{
    private const string BaseUrl = "https://generativelanguage.googleapis.com/v1beta";
    private const int MaxOutputTokens = 4000;

    private readonly HttpClient _http;
    private readonly string? _apiKey;
    private readonly string _model;
    private readonly ILogger<GeminiLessonPlanner> _logger;

    public GeminiLessonPlanner(HttpClient http, IConfiguration configuration, ILogger<GeminiLessonPlanner> logger)
    {
        _http = http;
        _apiKey = configuration["Ai:Gemini:ApiKey"]
            ?? Environment.GetEnvironmentVariable("GEMINI_API_KEY");
        _model = configuration["Ai:Gemini:Model"] ?? "gemini-2.0-flash";
        _logger = logger;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_apiKey);
    public string ProviderName => "Gemini";

    public async Task<LessonPlanResult> BuildAsync(LessonPlanRequest request)
    {
        if (!IsConfigured)
            return new LessonPlanResult(false, null, "התכונה אינה מוגדרת בשרת.");

        var body = new
        {
            system_instruction = new { parts = new[] { new { text = LessonPlanPrompt.System } } },
            contents = new[]
            {
                new { role = "user", parts = new[] { new { text = LessonPlanPrompt.Details(request) } } }
            },
            generationConfig = new { maxOutputTokens = MaxOutputTokens, temperature = 0.7 }
        };

        try
        {
            using var response = await _http.PostAsJsonAsync(
                $"{BaseUrl}/models/{_model}:generateContent?key={_apiKey}", body);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                await LogAvailableModelsAsync();
                return new LessonPlanResult(false, null,
                    "המודל המוגדר אינו זמין. יש לעדכן את ההגדרה Ai:Gemini:Model.");
            }

            if (!response.IsSuccessStatusCode)
            {
                // גוף השגיאה עשוי לכלול את המפתח בכתובת — נרשם בלוג בלבד
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("Gemini returned {Status}: {Body}", (int)response.StatusCode, Truncate(error));
                return response.StatusCode == HttpStatusCode.TooManyRequests
                    ? new LessonPlanResult(false, null, "חרגנו ממכסת השימוש החינמית. נסי שוב מאוחר יותר.")
                    : new LessonPlanResult(false, null, "בניית המערך נכשלה. נסי שוב בעוד רגע.");
            }

            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var plan = ExtractText(document.RootElement);

            return string.IsNullOrWhiteSpace(plan)
                ? new LessonPlanResult(false, null, "לא התקבל מערך שיעור. נסי שוב.")
                : new LessonPlanResult(true, plan, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lesson plan generation failed (Gemini).");
            return new LessonPlanResult(false, null, "בניית המערך נכשלה. נסי שוב בעוד רגע.");
        }
    }

    /// <summary>
    /// ‎candidates[0].content.parts[*].text‎, בהגנה מלאה: תשובה חסומה ע"י מסנן בטיחות
    /// מגיעה בלי ‎content‎ כלל, ולא כשגיאת HTTP.
    /// </summary>
    private static string ExtractText(JsonElement root)
    {
        if (!root.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0)
            return string.Empty;

        var first = candidates[0];
        if (!first.TryGetProperty("content", out var content) ||
            !content.TryGetProperty("parts", out var parts))
            return string.Empty;

        return string.Join("\n", parts.EnumerateArray()
            .Where(p => p.TryGetProperty("text", out _))
            .Select(p => p.GetProperty("text").GetString())
            .Where(t => !string.IsNullOrWhiteSpace(t))).Trim();
    }

    private async Task LogAvailableModelsAsync()
    {
        try
        {
            using var response = await _http.GetAsync($"{BaseUrl}/models?key={_apiKey}");
            if (!response.IsSuccessStatusCode) return;

            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            if (!document.RootElement.TryGetProperty("models", out var models)) return;

            var names = models.EnumerateArray()
                .Where(m => m.TryGetProperty("name", out _))
                .Select(m => m.GetProperty("name").GetString())
                .Where(n => n is not null);

            _logger.LogError(
                "Gemini model {Model} was not found. Models available to this key: {Available}",
                _model, string.Join(", ", names));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not list the models available to this Gemini key.");
        }
    }

    private static string Truncate(string value) => value.Length <= 500 ? value : value[..500];
}
