using Talmidon.Infrastructure.Ai;

namespace Talmidon.Tests;

/// <summary>
/// בחירת ספק ה-AI. זו נקודת ההחלטה שמאפשרת להחליף מודל בהגדרה במקום בקוד, ולכן היא
/// נבדקת בנפרד: כל השאר תלוי בכך שהיא בוחרת נכון.
/// </summary>
public class LessonPlannerSelectionTests
{
    [Fact]
    public void NoKeysAtAll_MeansTheFeatureIsOff()
    {
        Assert.Equal(LessonPlannerProvider.None, LessonPlannerSelection.Choose(null, null, null));
    }

    /// <summary>
    /// מפתח שמונח בסביבה אינו בקשה להשתמש בו. תכונה שמדליקה את עצמה כך עולה זמן וכסף
    /// למי שלא ביקש אותה, וכדי לכבות אותה צריך קודם לגלות שהיא פועלת.
    /// </summary>
    [Theory]
    [InlineData("g", null)]
    [InlineData(null, "a")]
    [InlineData("g", "a")]
    public void AKeyOnItsOwnDoesNotTurnTheFeatureOn(string? geminiKey, string? anthropicKey)
    {
        Assert.Equal(LessonPlannerProvider.None, LessonPlannerSelection.Choose(geminiKey, anthropicKey, null));
    }

    [Theory]
    [InlineData("anthropic", LessonPlannerProvider.Anthropic)]
    [InlineData("claude", LessonPlannerProvider.Anthropic)]
    [InlineData("Claude", LessonPlannerProvider.Anthropic)]
    [InlineData("  ANTHROPIC  ", LessonPlannerProvider.Anthropic)]
    [InlineData("gemini", LessonPlannerProvider.Gemini)]
    [InlineData("  Gemini ", LessonPlannerProvider.Gemini)]
    public void AskingForAProviderTurnsItOn(string configured, LessonPlannerProvider expected)
    {
        Assert.Equal(expected, LessonPlannerSelection.Choose("g", "a", configured));
    }

    /// <summary>"none" ושם שאינו מוכר מגיעים לאותו מקום — כבוי — ואף אחד מהם אינו מפיל את השרת.</summary>
    [Theory]
    [InlineData("none")]
    [InlineData("llama")]
    [InlineData("")]
    [InlineData("   ")]
    public void AnythingElseLeavesItOff(string configured)
    {
        Assert.Equal(LessonPlannerProvider.None, LessonPlannerSelection.Choose("g", "a", configured));
    }

    [Fact]
    public void UnavailablePlanner_ReportsItselfAsUnconfigured()
    {
        var planner = new UnavailableLessonPlanner();

        Assert.False(planner.IsConfigured);
        Assert.Equal("none", planner.ProviderName);
    }
}
