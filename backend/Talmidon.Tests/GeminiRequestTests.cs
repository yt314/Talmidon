using System.Text.Json;
using Talmidon.Infrastructure.Ai;

namespace Talmidon.Tests;

/// <summary>
/// מבנה הבקשה שנשלחת ל-Gemini.
///
/// אי אפשר לבדוק כאן מול הספק, ולכן זו הבדיקה היחידה שתתפוס שם שדה שגוי לפני שהוא מגיע
/// לייצור: הספק עונה על שדה לא מוכר בשגיאה כללית, ומורה שלוחצת על "בניית מערך" רואה רק
/// שזה לא עובד.
/// </summary>
public class GeminiRequestTests
{
    private static readonly LessonPlanRequest Sample =
        new("מתמטיקה", "שברים — חיבור וחיסור", 60, "כיתה ה'", "מתקשה בהמרת שברים");

    private static JsonElement Serialize(bool allowThinking)
    {
        var body = GeminiRequest.Build(Sample, 8192, 0.7, allowThinking);
        // אותן הגדרות שבהן PostAsJsonAsync משתמשת
        var json = JsonSerializer.Serialize(body, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        return JsonDocument.Parse(json).RootElement.Clone();
    }

    [Fact]
    public void CarriesTheSystemPromptAndTheLessonDetails()
    {
        var root = Serialize(allowThinking: true);

        var system = root.GetProperty("system_instruction").GetProperty("parts")[0].GetProperty("text").GetString();
        Assert.Contains("מערך שיעור", system);

        var content = root.GetProperty("contents")[0];
        Assert.Equal("user", content.GetProperty("role").GetString());

        var details = content.GetProperty("parts")[0].GetProperty("text").GetString();
        Assert.Contains("שברים — חיבור וחיסור", details);
        Assert.Contains("60", details);
        Assert.Contains("מתקשה בהמרת שברים", details);
    }

    /// <summary>להנחיית המערכת אין דובר, ושדה role ריק היה נדחה על ידי הספק.</summary>
    [Fact]
    public void TheSystemInstructionCarriesNoRole()
    {
        var root = Serialize(allowThinking: true);

        Assert.False(root.GetProperty("system_instruction").TryGetProperty("role", out _));
    }

    [Fact]
    public void SendsTheOutputBudgetAndTemperature()
    {
        var config = Serialize(allowThinking: true).GetProperty("generationConfig");

        Assert.Equal(8192, config.GetProperty("maxOutputTokens").GetInt32());
        Assert.Equal(0.7, config.GetProperty("temperature").GetDouble());
    }

    /// <summary>
    /// כשהחשיבה מותרת אין לשלוח את השדה כלל — מודלים ישנים אינם מכירים אותו ודוחים את
    /// הבקשה כולה.
    /// </summary>
    [Fact]
    public void ThinkingConfigIsAbsentUnlessThinkingIsTurnedOff()
    {
        Assert.False(Serialize(allowThinking: true).GetProperty("generationConfig")
            .TryGetProperty("thinkingConfig", out _));
    }

    [Fact]
    public void TurningThinkingOffSendsAZeroBudget()
    {
        var config = Serialize(allowThinking: false).GetProperty("generationConfig");

        Assert.Equal(0, config.GetProperty("thinkingConfig").GetProperty("thinkingBudget").GetInt32());
    }

    /// <summary>שדות ריקים אינם נשלחים כשורות ריקות בהנחיה.</summary>
    [Fact]
    public void OmittedOptionalFieldsDoNotAppearAsEmptyLines()
    {
        var body = GeminiRequest.Build(
            new LessonPlanRequest("אנגלית", "זמן עבר", 45, null, null), 8192, 0.7, true);
        var details = body.Contents[0].Parts[0].Text;

        Assert.DoesNotContain("כיתה/רמה:", details);
        Assert.DoesNotContain("מה שחשוב לדעת", details);
        Assert.Contains("זמן עבר", details);
    }
}
