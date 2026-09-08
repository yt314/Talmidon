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
/// שמות המודלים של Gemini מתחלפים בקצב מהיר, וכל מפתח רואה רשימה אחרת. לכן כשהשם
/// המוגדר אינו קיים אצל הספק, המימוש שואל אילו מודלים כן זמינים למפתח הזה, בוחר אחד
/// ומנסה שוב — במקום להשאיר את המורה מול הודעת שגיאה שאין לה דרך לתקן.
/// </summary>
public class GeminiLessonPlanner : ILessonPlanner
{
    private const string BaseUrl = "https://generativelanguage.googleapis.com/v1beta";
    private const int MaxOutputTokens = 4000;

    private readonly HttpClient _http;
    private readonly GeminiModelResolver _resolver;
    private readonly string? _apiKey;
    private readonly string _configuredModel;
    private readonly ILogger<GeminiLessonPlanner> _logger;

    public GeminiLessonPlanner(
        HttpClient http,
        GeminiModelResolver resolver,
        IConfiguration configuration,
        ILogger<GeminiLessonPlanner> logger)
    {
        _http = http;
        _resolver = resolver;
        _apiKey = configuration["Ai:Gemini:ApiKey"]
            ?? Environment.GetEnvironmentVariable("GEMINI_API_KEY");
        _configuredModel = configuration["Ai:Gemini:Model"] ?? "gemini-flash-latest";
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

        var model = _resolver.Resolved ?? _configuredModel;
        var attempt = await TryAsync(model, body);

        // שם שאינו קיים אצל הספק — שואלים מה כן זמין למפתח הזה ומנסים פעם אחת נוספת
        if (attempt.ModelMissing)
        {
            var discovered = await DiscoverModelAsync();
            if (discovered is null || discovered == model)
            {
                _logger.LogError(
                    "Gemini model {Model} is unavailable and no usable replacement was found for this key.", model);
                return new LessonPlanResult(false, null,
                    "המודל המוגדר אינו זמין. יש לעדכן את ההגדרה Ai:Gemini:Model.");
            }

            _logger.LogWarning("Gemini model {Model} is unavailable; using {Discovered} instead.", model, discovered);
            attempt = await TryAsync(discovered, body);
            model = discovered;
        }

        if (attempt.Succeeded) _resolver.Remember(model);

        return attempt.Result;
    }

    private readonly record struct Attempt(bool Succeeded, bool ModelMissing, LessonPlanResult Result);

    private async Task<Attempt> TryAsync(string model, object body)
    {
        try
        {
            using var response = await _http.PostAsJsonAsync(
                $"{BaseUrl}/models/{model}:generateContent?key={_apiKey}", body);

            if (response.StatusCode == HttpStatusCode.NotFound)
                return new Attempt(false, true, new LessonPlanResult(false, null, "המודל המוגדר אינו זמין."));

            if (!response.IsSuccessStatusCode)
            {
                // גוף השגיאה עשוי לכלול את המפתח בכתובת — נרשם בלוג בלבד
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("Gemini returned {Status}: {Body}", (int)response.StatusCode, Truncate(error));
                var message = response.StatusCode == HttpStatusCode.TooManyRequests
                    ? "חרגנו ממכסת השימוש החינמית. נסי שוב מאוחר יותר."
                    : "בניית המערך נכשלה. נסי שוב בעוד רגע.";
                return new Attempt(false, false, new LessonPlanResult(false, null, message));
            }

            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var plan = ExtractText(document.RootElement);

            return string.IsNullOrWhiteSpace(plan)
                ? new Attempt(false, false, new LessonPlanResult(false, null, "לא התקבל מערך שיעור. נסי שוב."))
                : new Attempt(true, false, new LessonPlanResult(true, plan, null));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lesson plan generation failed (Gemini).");
            return new Attempt(false, false, new LessonPlanResult(false, null, "בניית המערך נכשלה. נסי שוב בעוד רגע."));
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

    /// <summary>שואל את הספק אילו מודלים זמינים למפתח הזה, ובוחר אחד מהם.</summary>
    private async Task<string?> DiscoverModelAsync()
    {
        try
        {
            using var response = await _http.GetAsync($"{BaseUrl}/models?key={_apiKey}");
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Could not list Gemini models: {Status}.", (int)response.StatusCode);
                return null;
            }

            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            if (!document.RootElement.TryGetProperty("models", out var models)) return null;

            var available = models.EnumerateArray().Select(ToModelInfo).Where(m => m.Name.Length > 0).ToList();
            _logger.LogInformation(
                "Models available to this Gemini key: {Available}", string.Join(", ", available.Select(m => m.Name)));

            return GeminiModelChoice.Pick(available);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not list the models available to this Gemini key.");
            return null;
        }
    }

    private static GeminiModelInfo ToModelInfo(JsonElement model)
    {
        var name = model.TryGetProperty("name", out var nameElement) ? nameElement.GetString() ?? "" : "";

        // טיפוס מפורש ולא var: לענף הריק אין טיפוס טבעי משלו
        List<string> methods = model.TryGetProperty("supportedGenerationMethods", out var list) &&
                               list.ValueKind == JsonValueKind.Array
            ? list.EnumerateArray().Select(m => m.GetString() ?? "").ToList()
            : [];

        return new GeminiModelInfo(name, methods);
    }

    private static string Truncate(string value) => value.Length <= 500 ? value : value[..500];
}
