using System.Net;
using System.Net.Http.Json;
using Talmidon.Api.Controllers;
using Talmidon.Infrastructure.Data;

namespace Talmidon.Tests;

/// <summary>
/// נקודת סימן החיים. שתי התכונות שלה הן כל הסיבה שהיא קיימת: היא פתוחה בלי התחברות,
/// והיא אינה נוגעת בבסיס הנתונים.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class HealthEndpointTests(TalmidonWebApplicationFactory factory)
{
    private record HealthResponse(string Status, DateTimeOffset Utc);

    [Fact]
    public async Task HealthAnswersWithoutSigningIn()
    {
        var response = await factory.CreateClient().GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<HealthResponse>();
        Assert.Equal("ok", body!.Status);
    }

    /// <summary>
    /// השמירה האמיתית כאן היא על החשבון, לא על הקוד: המשימה המתוזמנת מושכת את הנתיב
    /// הזה כל עשר דקות. ברגע שמישהו יוסיף לו תלות בבסיס הנתונים — ולו בדיקה "קלה"
    /// שהחיבור חי — הפינג יחזיק גם את Neon ער מסביב לשעון וישרוף את מכסת החישוב
    /// החודשית. בדיקה על רשימת הפרמטרים של הבנאי היא הדרך היחידה לתפוס את זה
    /// לפני שזה קורה בשקט, בחשבון של מישהי אחרת.
    /// </summary>
    [Fact]
    public void HealthNeverTouchesTheDatabase()
    {
        var constructors = typeof(HealthController).GetConstructors();

        var dependencies = constructors
            .SelectMany(c => c.GetParameters())
            .Select(p => p.ParameterType)
            .ToList();

        Assert.DoesNotContain(typeof(TalmidonDbContext), dependencies);
        Assert.Empty(dependencies);
    }
}
