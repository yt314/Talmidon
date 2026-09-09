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

    /// <summary>
    /// שם מודל פסול חוזר מהספק גם כ-400 ולא רק כ-404. הענף הזה הוא מה שמפעיל את הגילוי
    /// האוטומטי, ובלעדיו התקלה שדווחה מהייצור נראית ככשל כללי ונשארת כזו.
    /// </summary>
    [Theory]
    [InlineData(404, "")]
    [InlineData(400, "{\"error\":{\"message\":\"models/gemini-9-flash is not found for API version v1beta\"}}")]
    [InlineData(400, "{\"error\":{\"message\":\"Model is not supported for generateContent\"}}")]
    [InlineData(400, "{\"error\":{\"status\":\"NOT_FOUND\"}}")]
    public void AModelProblemTriggersDiscovery(int status, string body)
    {
        Assert.True(GeminiFailure.IsModelProblem(status, body));
    }

    /// <summary>
    /// כשל שאינו קשור לשם המודל — מפתח פסול, מכסה, תקלה אצל הספק — אינו מפעיל גילוי:
    /// החלפת מודל לא תתקן אותו, והניסיון הנוסף רק יבזבז מכסה.
    /// </summary>
    [Theory]
    [InlineData(403, "{\"error\":{\"message\":\"API key not valid\"}}")]
    [InlineData(429, "{\"error\":{\"message\":\"Quota exceeded\"}}")]
    [InlineData(500, "{\"error\":{\"message\":\"Internal error\"}}")]
    [InlineData(400, "{\"error\":{\"message\":\"Invalid JSON payload\"}}")]
    public void AnUnrelatedFailureDoesNot(int status, string body)
    {
        Assert.False(GeminiFailure.IsModelProblem(status, body));
    }

    /// <summary>
    /// עומס אצל הספק חולף, ולכן ניסיון חוזר שווה משהו. מכסה שנגמרה אינה חולפת, וניסיון
    /// חוזר עליה רק שורף עוד מכסה — ההבחנה הזו היא כל מה שמפריד בין השתיים.
    /// </summary>
    [Theory]
    [InlineData(500)]
    [InlineData(502)]
    [InlineData(503)]
    [InlineData(504)]
    public void ServerSideFailuresAreWorthRetrying(int status)
    {
        Assert.True(GeminiFailure.IsTransient(status));
    }

    [Theory]
    [InlineData(429)]
    [InlineData(403)]
    [InlineData(400)]
    [InlineData(404)]
    public void EverythingElseIsNot(int status)
    {
        Assert.False(GeminiFailure.IsTransient(status));
    }

    /// <summary>
    /// הדירוג מחזיר את כל המתאימים ולא רק את הראשון, כדי שיהיה למי לפנות כשהמועדף עמוס.
    /// </summary>
    [Fact]
    public void RankKeepsEveryUsableModelInPreferenceOrder()
    {
        var ranked = GeminiModelChoice.Rank([
            Text("models/gemini-2.5-pro"),
            Text("models/gemini-embedding-001"),
            Text("models/gemini-2.5-flash"),
            Text("models/gemini-2.5-flash-image")
        ]);

        Assert.Equal(new[] { "gemini-2.5-flash", "gemini-2.5-pro" }, ranked);
    }

    /// <summary>
    /// כיבוי החשיבה הוא אופטימיזציה, לא דרישה. מודל שדוחה אותה צריך לקבל את הבקשה
    /// שוב בלי הכיבוי — ולא להיכשל על משהו שנועד רק לקצר את ההמתנה.
    /// </summary>
    [Theory]
    [InlineData(400, "{\"error\":{\"message\":\"Unknown name \\\"thinkingConfig\\\"\"}}")]
    [InlineData(400, "{\"error\":{\"message\":\"Budget 0 is invalid for thinking\"}}")]
    public void ARejectedThinkingSettingIsRecognised(int status, string body)
    {
        Assert.True(GeminiFailure.IsThinkingRejected(status, body));
    }

    [Theory]
    [InlineData(400, "{\"error\":{\"message\":\"Invalid JSON payload\"}}")]
    [InlineData(503, "{\"error\":{\"message\":\"The model is overloaded\"}}")]
    public void AnotherFailureIsNotMistakenForIt(int status, string body)
    {
        Assert.False(GeminiFailure.IsThinkingRejected(status, body));
    }

    /// <summary>
    /// התיאור הטכני מגיע למסך, ולכן הוא חייב להיות מה שהספק אמר — ובלי המפתח.
    /// </summary>
    [Fact]
    public void TheDescriptionQuotesTheProviderAndNamesTheModel()
    {
        var described = GeminiFailure.Describe(503, "gemini-2.5-flash",
            """{"error":{"code":503,"message":"The model is overloaded. Please try again later.","status":"UNAVAILABLE"}}""");

        Assert.Equal("gemini-2.5-flash: HTTP 503 The model is overloaded. Please try again later.", described);
    }

    [Fact]
    public void AnApiKeyInTheProviderMessageIsRedacted()
    {
        var described = GeminiFailure.Describe(403, "m",
            """{"error":{"message":"Requests to this API ?key=AIzaSyRealLookingKey are blocked."}}""");

        Assert.DoesNotContain("AIzaSyRealLookingKey", described);
        Assert.Contains("key=***", described);
    }

    /// <summary>גוף שאינו JSON עדיין שווה משהו — עדיף עליו מאשר על שום דבר.</summary>
    [Fact]
    public void ABodyThatIsNotJsonIsPassedThroughAsIs()
    {
        Assert.Equal("m: HTTP 500 upstream connect error", GeminiFailure.Describe(500, "m", "upstream connect error"));
    }

    /// <summary>
    /// כיבוי החשיבה נשלח רק למי שמקבל אותו. שליחה לכולם ותיקון אחרי כישלון עולה
    /// בקשה מיותרת בכל הפעלה, ובזמן שהמורה ממתינה.
    /// </summary>
    [Theory]
    [InlineData("gemini-2.5-flash")]
    [InlineData("gemini-2.5-flash-lite")]
    public void ThinkingCanBeTurnedOffOnFlash(string model)
    {
        Assert.True(GeminiModelChoice.SupportsThinkingBudget(model));
    }

    [Theory]
    [InlineData("gemini-2.5-pro")]       // מינימום גדול מאפס
    [InlineData("gemini-2.0-flash")]     // אינו מכיר את השדה
    [InlineData("gemini-flash-latest")]  // כינוי — לא ידוע לאן הוא מצביע
    public void AndNotOnModelsThatRefuseIt(string model)
    {
        Assert.False(GeminiModelChoice.SupportsThinkingBudget(model));
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
