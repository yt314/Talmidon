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

    [Fact]
    public void OnlyGeminiKey_SelectsGemini()
    {
        Assert.Equal(LessonPlannerProvider.Gemini, LessonPlannerSelection.Choose("g", null, null));
    }

    [Fact]
    public void OnlyAnthropicKey_SelectsAnthropic()
    {
        Assert.Equal(LessonPlannerProvider.Anthropic, LessonPlannerSelection.Choose(null, "a", null));
    }

    /// <summary>
    /// כששני המפתחות מוגדרים, החינמי מנצח — אחרת מפתח בתשלום שנשאר "לניסיון" היה
    /// מתחיל לחייב בלי שאיש שם לב.
    /// </summary>
    [Fact]
    public void BothKeys_PrefersTheFreeProvider()
    {
        Assert.Equal(LessonPlannerProvider.Gemini, LessonPlannerSelection.Choose("g", "a", null));
    }

    [Theory]
    [InlineData("anthropic", LessonPlannerProvider.Anthropic)]
    [InlineData("claude", LessonPlannerProvider.Anthropic)]
    [InlineData("Claude", LessonPlannerProvider.Anthropic)]
    [InlineData("  ANTHROPIC  ", LessonPlannerProvider.Anthropic)]
    [InlineData("gemini", LessonPlannerProvider.Gemini)]
    [InlineData("none", LessonPlannerProvider.None)]
    public void ExplicitProvider_Wins(string configured, LessonPlannerProvider expected)
    {
        Assert.Equal(expected, LessonPlannerSelection.Choose("g", "a", configured));
    }

    /// <summary>שם ספק שאינו מוכר אינו מפיל את השרת — נופלים חזרה לבחירה לפי מפתח.</summary>
    [Fact]
    public void UnknownProviderName_FallsBackToKeyOrder()
    {
        Assert.Equal(LessonPlannerProvider.Gemini, LessonPlannerSelection.Choose("g", "a", "llama"));
        Assert.Equal(LessonPlannerProvider.None, LessonPlannerSelection.Choose(null, null, "llama"));
    }

    [Fact]
    public void BlankKeys_CountAsMissing()
    {
        Assert.Equal(LessonPlannerProvider.None, LessonPlannerSelection.Choose("   ", "", null));
    }

    [Fact]
    public void UnavailablePlanner_ReportsItselfAsUnconfigured()
    {
        var planner = new UnavailableLessonPlanner();

        Assert.False(planner.IsConfigured);
        Assert.Equal("none", planner.ProviderName);
    }
}
