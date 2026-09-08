using System.Text.Json.Serialization;

namespace Talmidon.Infrastructure.Ai;

/// <summary>
/// גוף הבקשה ל-Gemini, בטיפוסים מפורשים.
///
/// לא טיפוסים אנונימיים מאחורי <c>object</c>: שם כל שדה על החוט נקבע כאן ונראה לעין,
/// במקום להיגזר בזמן ריצה מהטיפוס שהמסדר במקרה קיבל. זה גם מה שמאפשר לבדוק את המבנה
/// בלי לפנות לספק — שגיאת שם שדה מתגלה בבדיקה ולא בייצור.
/// </summary>
public static class GeminiRequest
{
    public static RequestBody Build(LessonPlanRequest request, int maxOutputTokens, double temperature, bool allowThinking) =>
        new(
            new Content([new Part(LessonPlanPrompt.System)]),
            [new Content([new Part(LessonPlanPrompt.Details(request))], "user")],
            new GenerationConfig(maxOutputTokens, temperature, allowThinking ? null : new ThinkingConfig(0)));

    public sealed record RequestBody(
        [property: JsonPropertyName("system_instruction")] Content SystemInstruction,
        [property: JsonPropertyName("contents")] Content[] Contents,
        [property: JsonPropertyName("generationConfig")] GenerationConfig GenerationConfig);

    public sealed record Content(
        [property: JsonPropertyName("parts")] Part[] Parts,
        [property: JsonPropertyName("role"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        string? Role = null);

    public sealed record Part([property: JsonPropertyName("text")] string Text);

    public sealed record GenerationConfig(
        [property: JsonPropertyName("maxOutputTokens")] int MaxOutputTokens,
        [property: JsonPropertyName("temperature")] double Temperature,
        [property: JsonPropertyName("thinkingConfig"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        ThinkingConfig? Thinking = null);

    /// <summary>
    /// תקציב חשיבה אפס = המודל עונה מיד. המודלים החדשים "חושבים" לפני שהם עונים, והחשיבה
    /// נגרעת מאותו תקציב פלט — כך שמודל כזה יכול לכלות את כולו לפני שנכתבה מילה אחת.
    /// השדה אינו מוכר למודלים ישנים, ולכן נשלח רק כשמכבים את החשיבה בכוונה.
    /// </summary>
    public sealed record ThinkingConfig([property: JsonPropertyName("thinkingBudget")] int ThinkingBudget);
}
