using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Talmidon.Tests;

/// <summary>
/// המגבלה על /api/auth/* (Program.cs, מדיניות "auth") מעולם לא נבדקה: ה-fixture המשותף
/// (TalmidonWebApplicationFactory) מנפח אותה בכוונה ל-100000 כדי לא להפריע לשאר הבדיקות, כי כל
/// הבקשות מגיעות דרך TestServer עם "כתובת מקור" זהה. כדי לבדוק את ההגבלה עצמה צריך host נפרד
/// עם מכסה קטנה משלו — לא ניתן להשתמש ב-fixture המשותף לזה.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class RateLimitingTests
{
    [Fact]
    public async Task Login_ExceedingThePermitLimit_Returns429()
    {
        using var factory = new RateLimitedFactory(permitLimit: 3);
        var client = factory.CreateClient();

        for (var i = 0; i < 3; i++)
        {
            var response = await client.PostAsJsonAsync("/api/auth/login", new { email = "nobody@example.com", password = "WrongPass123" });
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        var overLimit = await client.PostAsJsonAsync("/api/auth/login", new { email = "nobody@example.com", password = "WrongPass123" });
        Assert.Equal(HttpStatusCode.TooManyRequests, overLimit.StatusCode);
    }

    /// <summary>
    /// אותה תשתית כמו TalmidonWebApplicationFactory, רק עם מכסת קצב אמיתית (קטנה) במקום המנופחת.
    /// לא יורשת ממנה ולא נוגעת ב-fixture המשותף — אחרת ה-Environment.SetEnvironmentVariable
    /// הגלובלי של הבנאי שם היה דורס בחזרה את המכסה הקטנה שכאן.
    /// </summary>
    private sealed class RateLimitedFactory : WebApplicationFactory<Program>
    {
        public RateLimitedFactory(int permitLimit)
        {
            Environment.SetEnvironmentVariable("ConnectionStrings__Default", TalmidonWebApplicationFactory.TestConnectionString);
            Environment.SetEnvironmentVariable("Jwt__SecretKey", "test-only-signing-key-not-for-production-0123456789ABCDEF");
            Environment.SetEnvironmentVariable("App__ClientUrl", "http://localhost:4200");
            Environment.SetEnvironmentVariable("RateLimiting__Auth__PermitLimit", permitLimit.ToString());
            Environment.SetEnvironmentVariable("RateLimiting__Auth__WindowMinutes", "1");
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.UseEnvironment("Development");
    }
}
