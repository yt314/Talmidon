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

    /// <summary>
    /// המתנות בין ניסיונות חוזרים. השכבה החינמית מחזירה "עמוס" לעיתים קרובות, ושתי
    /// המתנות קצרות פותרות את רוב המקרים — בלי להאריך את ההמתנה של המורה מעבר לסביר.
    /// </summary>
    private static readonly int[] RetryDelaysMs = [800, 2500];

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
        // מודל יציב שידוע שמקבל את כיבוי החשיבה, ולא כינוי שאיננו יודעים לאן הוא מצביע.
        // אם הוא אינו זמין למפתח הזה, הגילוי האוטומטי ימצא תחליף.
        _configuredModel = configuration["Ai:Gemini:Model"] ?? "gemini-2.5-flash";
        _logger = logger;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_apiKey);
    public string ProviderName => "Gemini";

    public async Task<LessonPlanResult> BuildAsync(LessonPlanRequest request)
    {
        if (!IsConfigured)
            return new LessonPlanResult(false, null, "התכונה אינה מוגדרת בשרת.");

        var model = _resolver.Resolved ?? _configuredModel;

        // חשיבה כבויה כברירת מחדל. המודלים החדשים "חושבים" לפני שהם עונים, וזה מוסיף
        // עשרות שניות להמתנה של המורה — על משימה שהיא טקסט קצר ומובנה לפי מבנה נתון,
        // ולא בעיה שדורשת מחשבה. מי שהמתין דקה למערך לא ינסה שוב.
        var thinking = LetItThink(model);
        var attempt = await TryWithRetriesAsync(model, BuildBody(request, thinking));

        // מודל שאינו מכיר את השדה, או שאינו מרשה אפס — מוותרים על הכיבוי ולא נכשלים
        if (attempt.ThinkingRejected)
        {
            _logger.LogWarning("Gemini model {Model} rejected the thinking setting; letting it think.", model);
            _resolver.RememberThinkingRequired();
            thinking = true;
            attempt = await TryWithRetriesAsync(model, BuildBody(request, allowThinking: true));
        }

        // שם שאינו קיים אצל הספק — שואלים מה כן זמין למפתח הזה ומנסים פעם אחת נוספת
        if (attempt.ModelMissing)
        {
            var discovered = (await DiscoverModelsAsync()).FirstOrDefault(m => m != model);
            if (discovered is null)
            {
                _logger.LogError(
                    "Gemini model {Model} is unavailable and no usable replacement was found for this key.", model);
                return new LessonPlanResult(false, null,
                    "המודל המוגדר אינו זמין. יש לעדכן את ההגדרה Ai:Gemini:Model.");
            }

            _logger.LogWarning("Gemini model {Model} is unavailable; using {Discovered} instead.", model, discovered);
            model = discovered;
            thinking = LetItThink(model);
            attempt = await TryWithRetriesAsync(model, BuildBody(request, thinking));
        }

        // עומס אצל הספק הוא לכל מודל בנפרד. אחרי שהניסיונות החוזרים על המודל הזה מוצו,
        // מודל אחר הוא עדיין סיכוי אמיתי — ועדיף על לשלוח את המורה ללחוץ שוב בעצמה.
        if (attempt.ProviderBusy)
        {
            var alternative = (await DiscoverModelsAsync()).FirstOrDefault(m => m != model);
            if (alternative is not null)
            {
                _logger.LogWarning("Gemini model {Model} is overloaded; trying {Alternative}.", model, alternative);
                var viaAlternative = await TryWithRetriesAsync(alternative, BuildBody(request, LetItThink(alternative)));
                if (!viaAlternative.ProviderBusy)
                {
                    attempt = viaAlternative;
                    model = alternative;
                    thinking = LetItThink(model);
                }
            }
        }

        // המודלים החדשים "חושבים" לפני שהם עונים, והחשיבה נגרעת מאותו תקציב פלט. מודל
        // כזה יכול לכלות את כולו לפני שנכתבה מילה אחת ולהחזיר תשובה ריקה בהצלחה מדומה,
        // ולכן ניסיון שני בלי חשיבה עדיף על הודעת שגיאה למורה.
        if (attempt.SpentBudgetBeforeAnswering && thinking)
        {
            _logger.LogWarning(
                "Gemini model {Model} used its whole output budget before writing anything; retrying without thinking.",
                model);
            attempt = await TryWithRetriesAsync(model, BuildBody(request, allowThinking: false));
        }

        if (attempt.Succeeded)
        {
            _resolver.Remember(model);
            _logger.LogInformation("Lesson plan built with Gemini model {Model}.", model);
        }

        return attempt.Result;
    }

    /// <summary>
    /// האם להשאיר את החשיבה דלוקה למודל הזה. כבויה כשאפשר — היא עולה עשרות שניות על
    /// משימה שהיא כתיבה לפי מבנה נתון — ודלוקה כשהמודל אינו מקבל את הכיבוי, או כשכבר
    /// דחה אותו פעם אחת.
    /// </summary>
    private bool LetItThink(string model) =>
        _resolver.ThinkingRequired || !GeminiModelChoice.SupportsThinkingBudget(model);

    private static GeminiRequest.RequestBody BuildBody(LessonPlanRequest request, bool allowThinking) =>
        GeminiRequest.Build(request, MaxOutputTokens, Temperature, allowThinking);

    private readonly record struct Attempt(
        bool Succeeded,
        bool ModelMissing,
        bool SpentBudgetBeforeAnswering,
        bool ProviderBusy,
        bool ThinkingRejected,
        LessonPlanResult Result);

    /// <summary>
    /// מנסה שוב כשהספק עמוס. עומס הוא המצב השכיח ביותר בשכבה החינמית, והוא חולף תוך
    /// שניות — הודעת שגיאה עליו שולחת את המורה ללחוץ שוב על מה שהשרת יכול לעשות לבדו.
    /// </summary>
    private async Task<Attempt> TryWithRetriesAsync(string model, GeminiRequest.RequestBody body)
    {
        var attempt = await TryAsync(model, body);

        foreach (var delay in RetryDelaysMs)
        {
            if (!attempt.ProviderBusy) return attempt;

            await Task.Delay(delay);
            _logger.LogWarning("Retrying Gemini model {Model} after a transient failure.", model);
            attempt = await TryAsync(model, body);
        }

        return attempt;
    }

    private async Task<Attempt> TryAsync(string model, GeminiRequest.RequestBody body)
    {
        try
        {
            using var response = await _http.PostAsJsonAsync(
                $"{BaseUrl}/models/{model}:generateContent?key={_apiKey}", body);

            if (!response.IsSuccessStatusCode)
            {
                var status = (int)response.StatusCode;
                // גוף השגיאה עשוי לכלול את המפתח בכתובת — נרשם בלוג בלבד
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError(
                    "Gemini returned {Status} for model {Model}: {Body}", status, model, Truncate(error));

                var detail = GeminiFailure.Describe(status, model, error);

                if (GeminiFailure.IsModelProblem(status, error))
                    return Failed(true, false, "המודל המוגדר אינו זמין.", detail: detail);

                if (GeminiFailure.IsThinkingRejected(status, error))
                    return Failed(false, false, "המודל אינו מקבל את ההגדרה הזו.",
                        thinkingRejected: true, detail: detail);

                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                    return Failed(false, false, "חרגנו ממכסת השימוש החינמית. נסי שוב מאוחר יותר.", detail: detail);

                if (GeminiFailure.IsTransient(status))
                    return Failed(false, false,
                        "השירות של Gemini עמוס כרגע. ניסינו כמה פעמים — כדאי לנסות שוב בעוד דקה.",
                        providerBusy: true, detail: detail);

                // מספר השגיאה מוצג בכוונה: בלעדיו כל כשל מהספק נראה זהה, ואי אפשר לדעת
                // מהמסך אם מדובר במפתח, במכסה או בתקלה זמנית אצלו.
                return Failed(false, false, $"בניית המערך נכשלה (שגיאה {status} מהספק). נסי שוב בעוד רגע.",
                    detail: detail);
            }

            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var answer = ReadAnswer(document.RootElement);

            if (!string.IsNullOrWhiteSpace(answer.Text))
                return new Attempt(true, false, false, false, false, new LessonPlanResult(true, answer.Text, null));

            // תשובה ריקה מגיעה כהצלחה, ולכן חייבים לקרוא את הסיבה כדי לדעת מה קרה
            _logger.LogWarning(
                "Gemini model {Model} returned no text (finishReason={Finish}, blockReason={Block}).",
                model, answer.FinishReason ?? "-", answer.BlockReason ?? "-");

            var emptyDetail = $"{model}: no text (finishReason={answer.FinishReason ?? "-"}, " +
                              $"blockReason={answer.BlockReason ?? "-"})";

            if (answer.BlockReason is not null || answer.FinishReason is "SAFETY" or "PROHIBITED_CONTENT")
                return Failed(false, false,
                    "הבקשה נחסמה על ידי מסנן התוכן של הספק. נסי לנסח את נושא השיעור אחרת.", detail: emptyDetail);

            if (answer.FinishReason == "MAX_TOKENS")
                return Failed(false, true, "התשובה נקטעה לפני שנכתב דבר. נסי שוב.", detail: emptyDetail);

            return Failed(false, false, "לא התקבל מערך שיעור. נסי שוב.", detail: emptyDetail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lesson plan generation failed (Gemini).");
            // חיבור שנקטע הוא חולף בדיוק כמו עומס, ולכן גם הוא ראוי לניסיון נוסף
            return Failed(false, false, "לא הצלחנו להגיע לשירות בניית המערכים. נסי שוב בעוד רגע.",
                providerBusy: true, detail: $"{model}: {ex.GetType().Name} — {ex.Message}");
        }
    }

    private static Attempt Failed(
        bool modelMissing, bool spentBudget, string message,
        bool providerBusy = false, bool thinkingRejected = false, string? detail = null) =>
        new(false, modelMissing, spentBudget, providerBusy, thinkingRejected,
            new LessonPlanResult(false, null, message, detail));

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

    /// <summary>
    /// שואל את הספק אילו מודלים זמינים למפתח הזה, מדורגים מהמועדף ומטה. הרשימה נשמרת
    /// לאורך חיי התהליך: היא כמעט אינה משתנה, ואין טעם לשאול שוב בכל כשל.
    /// </summary>
    private async Task<IReadOnlyList<string>> DiscoverModelsAsync()
    {
        if (_resolver.Available is { Count: > 0 } cached) return cached;

        try
        {
            using var response = await _http.GetAsync($"{BaseUrl}/models?key={_apiKey}");
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Could not list Gemini models: {Status}.", (int)response.StatusCode);
                return [];
            }

            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            if (!document.RootElement.TryGetProperty("models", out var models)) return [];

            var available = models.EnumerateArray().Select(ToModelInfo).Where(m => m.Name.Length > 0).ToList();
            _logger.LogInformation(
                "Models available to this Gemini key: {Available}", string.Join(", ", available.Select(m => m.Name)));

            var ranked = GeminiModelChoice.Rank(available);
            _resolver.RememberAvailable(ranked);
            return ranked;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not list the models available to this Gemini key.");
            return [];
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
