using Talmidon.Infrastructure.Diagnostics;

namespace Talmidon.Tests;

/// <summary>
/// יומן התקלות נשמר במסד ונקרא במסך. שני הדברים שחייבים להיות נכונים לפני שמכניסים
/// אליו הודעות שגיאה של ספקים חיצוניים: שהסודות שבהן לא נשמרים, ושהגודל חסום.
/// </summary>
public class ErrorDetailTests
{
    [Theory]
    [InlineData("Host=db;Username=talmidon;Password=hunter2", "hunter2")]
    [InlineData("{\"apiKey\":\"sk-live-abcdef\"}", "sk-live-abcdef")]
    [InlineData("api_key=AIzaSyFAKE123", "AIzaSyFAKE123")]
    [InlineData("Authorization: Bearer eyJhbGciOiJIUzI1NiJ9", "eyJhbGciOiJIUzI1NiJ9")]
    [InlineData("refresh_token=9f8e7d6c", "9f8e7d6c")]
    public void SecretsAreNotKept(string text, string secret)
    {
        var cleaned = ErrorDetail.Redact(text);

        Assert.DoesNotContain(secret, cleaned);
        Assert.Contains("***", cleaned);
    }

    /// <summary>הניקוי לא אמור לאכול את מה שבגללו פותחים את השורה מלכתחילה.</summary>
    [Fact]
    public void OrdinaryTextSurvivesRedaction()
    {
        const string text = "Npgsql.PostgresException: duplicate key value violates unique constraint \"IX_Students_UserId\"";

        Assert.Equal(text, ErrorDetail.Redact(text));
    }

    [Fact]
    public void DetailCarriesTheInnerException()
    {
        var exception = new InvalidOperationException("השמירה נכשלה", new TimeoutException("אין תשובה מהמסד"));

        Assert.Contains("אין תשובה מהמסד", ErrorDetail.Detail(exception));
    }

    /// <summary>
    /// עקבת מחסנית של חריגה עמוקה עוברת בקלות את גבול העמודה. חיתוך כאן עדיף על
    /// כישלון שמירה — שורה חתוכה אפשר לקרוא, שורה שלא נשמרה לא.
    /// </summary>
    [Fact]
    public void LongTextIsCutToWhatTheColumnHolds()
    {
        var exception = new InvalidOperationException(new string('א', 10_000));

        Assert.True(ErrorDetail.Message(exception).Length <= ErrorDetail.MaxMessageLength);
        Assert.True(ErrorDetail.Detail(exception).Length <= ErrorDetail.MaxDetailLength);
    }
}
