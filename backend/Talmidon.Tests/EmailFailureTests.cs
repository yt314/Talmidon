using System.Net.Http.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Talmidon.Infrastructure.Data;
using Talmidon.Infrastructure.Email;

namespace Talmidon.Tests;

/// <summary>
/// מייל שנכשל בשליחה היה נכתב ליומן של ספק האירוח ונעלם. זה החמור שבכישלונות
/// השקטים: מורה שנרשמה ולא קיבלה מייל אימות פשוט לא נכנסת, ואין שום סימן לכך
/// בשום מקום שמישהו מסתכל בו. עכשיו זה שורה ביומן התקלות.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class EmailFailureTests(TalmidonWebApplicationFactory factory)
{
    private sealed class ThrowingEmailSender : IEmailSender
    {
        public Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default) =>
            throw new InvalidOperationException("בדיקה: שרת הדואר אינו זמין");
    }

    [Fact]
    public async Task AConfirmationEmailThatFailsIsRecorded()
    {
        await using var brokenEmail = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.AddSingleton<IEmailSender>(new ThrowingEmailSender())));

        var response = await brokenEmail.CreateClient().PostAsJsonAsync("/api/auth/register", new
        {
            email = TestHelpers.UniqueEmail("email-failure"),
            password = "TestPass123",
            fullName = "מורה בדיקה",
            phone = (string?)null
        });

        // ההרשמה עצמה מצליחה — כישלון במייל אינו סיבה להפיל אותה, והחשבון אכן נוצר.
        response.EnsureSuccessStatusCode();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TalmidonDbContext>();
        var recorded = await db.ErrorLog
            .Where(e => e.Path == "auth/confirmation")
            .OrderByDescending(e => e.OccurredAt)
            .FirstOrDefaultAsync();

        Assert.NotNull(recorded);
        Assert.Equal("email", recorded!.Source);
        Assert.Contains("שרת הדואר אינו זמין", recorded.Message);
    }
}
