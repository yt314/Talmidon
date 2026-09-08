using Talmidon.Infrastructure.Ai;

namespace Talmidon.Tests;

/// <summary>
/// בחירת מודל Gemini מתוך מה שהמפתח באמת רשאי להשתמש בו.
///
/// זו נקודת הכשל שנצפתה בייצור: השם שהוגדר לא היה קיים אצל הספק, והמורה קיבלה שגיאה
/// שאין לה דרך לתקן. הבחירה נבדקת כאן בנפרד, בלי רשת, כי היא מה שהופך את התקלה הזו
/// למשהו שהשרת פותר בעצמו.
/// </summary>
public class GeminiModelChoiceTests
{
    private static GeminiModelInfo Text(string name) => new(name, ["generateContent", "countTokens"]);

    [Fact]
    public void PrefersFlashOverPro()
    {
        var picked = GeminiModelChoice.Pick([Text("models/gemini-2.5-pro"), Text("models/gemini-2.5-flash")]);

        Assert.Equal("gemini-2.5-flash", picked);
    }

    /// <summary>הכינוי "latest" עוקב אחרי הגרסה הנוכחית לבדו, ולכן הוא עדיף על שם נעוץ.</summary>
    [Fact]
    public void PrefersTheLatestAlias()
    {
        var picked = GeminiModelChoice.Pick([Text("models/gemini-2.5-flash"), Text("models/gemini-flash-latest")]);

        Assert.Equal("gemini-flash-latest", picked);
    }

    /// <summary>גרסאות preview נעלמות בלי הודעה — הן הבחירה האחרונה, לא הראשונה.</summary>
    [Fact]
    public void PrefersAStableModelOverAPreview()
    {
        var picked = GeminiModelChoice.Pick([
            Text("models/gemini-3-flash-preview"),
            Text("models/gemini-2.5-flash")
        ]);

        Assert.Equal("gemini-2.5-flash", picked);
    }

    [Fact]
    public void PrefersTheNewerVersionWhenNothingElseSeparatesThem()
    {
        var picked = GeminiModelChoice.Pick([Text("models/gemini-2.0-flash"), Text("models/gemini-2.5-flash")]);

        Assert.Equal("gemini-2.5-flash", picked);
    }

    /// <summary>
    /// מודלים שאינם מייצרים טקסט אינם נבחרים גם כשהם מדווחים על generateContent —
    /// מודל תמונה היה מחזיר "מערך שיעור" ריק, וזו תקלה שקשה יותר לאבחן משגיאה.
    /// </summary>
    [Fact]
    public void SkipsModelsThatDoNotProduceText()
    {
        var picked = GeminiModelChoice.Pick([
            Text("models/gemini-2.5-flash-image"),
            Text("models/imagen-4.0-generate-001"),
            Text("models/gemini-2.5-flash-native-audio"),
            Text("models/gemini-2.5-pro")
        ]);

        Assert.Equal("gemini-2.5-pro", picked);
    }

    [Fact]
    public void SkipsModelsWithoutGenerateContent()
    {
        var picked = GeminiModelChoice.Pick([
            new GeminiModelInfo("models/gemini-embedding-001", ["embedContent"]),
            new GeminiModelInfo("models/text-bison", ["countTokens"])
        ]);

        Assert.Null(picked);
    }

    [Fact]
    public void NothingAvailableMeansNoChoice()
    {
        Assert.Null(GeminiModelChoice.Pick([]));
    }

    /// <summary>אם אין flash ואין pro — עדיין עדיף לנסות משהו מאשר להיכשל.</summary>
    [Fact]
    public void FallsBackToWhateverGeneratesText()
    {
        var picked = GeminiModelChoice.Pick([Text("models/learnlm-2.0")]);

        Assert.Equal("learnlm-2.0", picked);
    }
}
