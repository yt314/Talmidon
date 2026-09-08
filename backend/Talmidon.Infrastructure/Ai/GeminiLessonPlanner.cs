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

    /// <summary>
    /// תקציב הפלט. מערך שיעור מלא בעברית הוא ארוך, ועברית נחתכת לאסימונים בצפיפות —
    /// תקציב צר החזיר תשובה קטועה, כלומר כלום.
    /// </summary>
    private const int MaxOutputTokens = 8192;
    private const double Temperature = 0.7;

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

        var model = _resolver.Resolved ?? _configuredModel;
        var attempt = await TryAsync(model, BuildBody(request, allowThinking: true));

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
            attempt = await TryAsync(discovered, BuildBody(request, allowThinking: true));
            model = discovered;
        }

        // המודלים החדשים "חושבים" לפני שהם עונים, והחשיבה נגרעת מאותו תקציב פלט. מודל
        // כזה יכול לכלות את כולו לפני שנכתבה מילה אחת ולהחזיר תשובה ריקה בהצלחה מדומה,
        // ולכן ניסיון שני בלי חשיבה עדיף על הודעת שגיאה למורה.
        if (attempt.SpentBudgetBeforeAnswering)
        {
            _logger.LogWarning(
                "Gemini model {Model} used its whole output budget before writing anything; retrying without thinking.",
                model);
            attempt = await TryAsync(model, BuildBody(request, allowThinking: false));
        }

        if (attempt.Succeeded)
        {
            _resolver.Remember(model);
            _logger.LogInformation("Lesson plan built with Gemini model {Model}.", model);
        }

        return attempt.Result;
    }

    private static GeminiRequest.RequestBody BuildBody(LessonPlanRequest request, bool allowThinking) =>
        GeminiRequest.Build(request, MaxOutputTokens, Temperature, allowThinking);

    private readonly record struct Attempt(
        bool Succeeded, bool ModelMissing, bool SpentBudgetBeforeAnswering, LessonPlanResult Result);

    private async Task<Attempt> TryAsync(string model, GeminiRequest.RequestBody body)
    {
        try
        {
            using var response = await _http.PostAsJsonAsync(
                $"{BaseUrl}/models/{model}:generateContent?key={_apiKey}", body);

            if (response.StatusCode == HttpStatusCode.NotFound)
                return Failed(true, false, "המודל המוגדר אינו זמין.");

            if (!response.IsSuccessStatusCode)
            {
                // גוף השגיאה עשוי לכלול את המפתח בכתובת — נרשם בלוג בלבד
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("Gemini returned {Status}: {Body}", (int)response.StatusCode, Truncate(error));
                return Failed(false, false, response.StatusCode == HttpStatusCode.TooManyRequests
                    ? "חרגנו ממכסת השימוש החינמית. נסי שוב מאוחר יותר."
                    : "בניית המערך נכשלה. נסי שוב בעוד רגע.");
            }

            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var answer = ReadAnswer(document.RootElement);

            if (!string.IsNullOrWhiteSpace(answer.Text))
                return new Attempt(true, false, false, new LessonPlanResult(true, answer.Text, null));

            // תשובה ריקה מגיעה כהצלחה, ולכן חייבים לקרוא את הסיבה כדי לדעת מה קרה
            _logger.LogWarning(
                "Gemini model {Model} returned no text (finishReason={Finish}, blockReason={Block}).",
                model, answer.FinishReason ?? "-", answer.BlockReason ?? "-");

            if (answer.BlockReason is not null || answer.FinishReason is "SAFETY" or "PROHIBITED_CONTENT")
                return Failed(false, false, "הבקשה נחסמה על ידי מסנן התוכן של הספק. נסי לנסח את נושא השיעור אחרת.");

            if (answer.FinishReason == "MAX_TOKENS")
                return Failed(false, true, "התשובה נקטעה לפני שנכתב דבר. נסי שוב.");

            return Failed(false, false, "לא התקבל מערך שיעור. נסי שוב.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lesson plan generation failed (Gemini).");
            return Failed(false, false, "בניית המערך נכשלה. נסי שוב בעוד רגע.");
        }
    }

    private static Attempt Failed(bool modelMissing, bool spentBudget, string message) =>
        new(false, modelMissing, spentBudget, new LessonPlanResult(false, null, message));

    private readonly record struct Answer(string Text, string? FinishReason, string? BlockReason);

    /// <summary>
    /// ‎candidates[0].content.parts[*].text‎, בהגנה מלאה בכל רמה: תשובה שנחסמה או שנקטעה
    /// מגיעה כ-200 בלי ‎content‎ כלל, ולא כשגיאת HTTP. הסיבה נמצאת ב-finishReason או
    /// ב-promptFeedback, ובלעדיה כל כישלון כזה נראה אותו הדבר.
    /// </summary>
    private static Answer ReadAnswer(JsonElement root)
    {
        var blockReason = root.TryGetProperty("promptFeedback", out var feedback) &&
                          feedback.TryGetProperty("blockReason", out var block)
            ? block.GetString()
            : null;

        if (!root.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0)
            return new Answer(string.Empty, null, blockReason);

        var first = candidates[0];
        var finishReason = first.TryGetProperty("finishReason", out var finish) ? finish.GetString() : null;

        if (!first.TryGetProperty("content", out var content) ||
            !content.TryGetProperty("parts", out var parts) ||
            parts.ValueKind != JsonValueKind.Array)
            return new Answer(string.Empty, finishReason, blockReason);

        var text = string.Join("\n", parts.EnumerateArray()
            .Where(p => p.TryGetProperty("text", out _))
            .Select(p => p.GetProperty("text").GetString())
            .Where(t => !string.IsNullOrWhiteSpace(t))).Trim();

        return new Answer(text, finishReason, blockReason);
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
