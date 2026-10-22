using System.Net;
using System.Net.Http.Json;

namespace Talmidon.Tests;

/// <summary>
/// שני נתיבי auth שלא היו מכוסים: התנתקות (שלילת אסימון רענון — רק של הבעלים) ושליחה
/// חוזרת של מייל האימות (תגובה זהה בכל מקרה, כדי לא לחשוף אם הכתובת רשומה).
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class LogoutAndResendTests(TalmidonWebApplicationFactory factory)
{
    private const string Password = "TestPass123";

    [Fact]
    public async Task Logout_RevokesTheCallersRefreshToken()
    {
        var (client, login) = await LoggedInTeacherAsync("logout-own");
        var authorized = TestHelpers.AuthorizedClient(factory, login.AccessToken);

        var logout = await authorized.PostAsJsonAsync("/api/auth/logout", new { refreshToken = login.RefreshToken });
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

        var refresh = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = login.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    [Fact]
    public async Task Logout_WithSomeoneElsesRefreshToken_LeavesItWorking()
    {
        var (victimClient, victim) = await LoggedInTeacherAsync("logout-victim");
        var (_, attacker) = await LoggedInTeacherAsync("logout-attacker");
        var attackerClient = TestHelpers.AuthorizedClient(factory, attacker.AccessToken);

        var logout = await attackerClient.PostAsJsonAsync("/api/auth/logout", new { refreshToken = victim.RefreshToken });
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

        var refresh = await victimClient.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = victim.RefreshToken });
        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
    }

    [Fact]
    public async Task Logout_WithoutBeingLoggedIn_IsRejected()
    {
        var (client, login) = await LoggedInTeacherAsync("logout-anon");

        var logout = await client.PostAsJsonAsync("/api/auth/logout", new { refreshToken = login.RefreshToken });

        Assert.Equal(HttpStatusCode.Unauthorized, logout.StatusCode);
    }

    [Fact]
    public async Task ResendConfirmation_SendsAnotherEmail_OnlyToAnUnconfirmedAccount()
    {
        var client = factory.CreateClient();
        var unconfirmed = TestHelpers.UniqueEmail("resend-unconfirmed");
        (await client.PostAsJsonAsync("/api/auth/register", new { email = unconfirmed, password = Password, fullName = "לא מאומתת" }))
            .EnsureSuccessStatusCode();
        var confirmed = TestHelpers.UniqueEmail("resend-confirmed");
        await TestHelpers.RegisterAndConfirmTeacherAsync(factory, client, confirmed, Password, "מאומתת");

        var toUnconfirmed = await client.PostAsJsonAsync("/api/auth/resend-confirmation", new { email = unconfirmed });
        var toConfirmed = await client.PostAsJsonAsync("/api/auth/resend-confirmation", new { email = confirmed });

        Assert.Equal(2, factory.SentEmails.To(unconfirmed).Count);
        Assert.Single(factory.SentEmails.To(confirmed));
        Assert.Equal(await toUnconfirmed.Content.ReadAsStringAsync(), await toConfirmed.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ResendConfirmation_ForAnUnknownAddress_AnswersTheSameAndSendsNothing()
    {
        var client = factory.CreateClient();
        var known = TestHelpers.UniqueEmail("resend-known");
        (await client.PostAsJsonAsync("/api/auth/register", new { email = known, password = Password, fullName = "רשומה" }))
            .EnsureSuccessStatusCode();
        var unknown = TestHelpers.UniqueEmail("resend-unknown");

        var toKnown = await client.PostAsJsonAsync("/api/auth/resend-confirmation", new { email = known });
        var toUnknown = await client.PostAsJsonAsync("/api/auth/resend-confirmation", new { email = unknown });

        Assert.Equal(HttpStatusCode.OK, toUnknown.StatusCode);
        Assert.Equal(await toKnown.Content.ReadAsStringAsync(), await toUnknown.Content.ReadAsStringAsync());
        Assert.Empty(factory.SentEmails.To(unknown));
    }

    private async Task<(HttpClient Client, LoginBody Login)> LoggedInTeacherAsync(string prefix)
    {
        var client = factory.CreateClient();
        var email = TestHelpers.UniqueEmail(prefix);
        await TestHelpers.RegisterAndConfirmTeacherAsync(factory, client, email, Password, "מורה בדיקה");

        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        response.EnsureSuccessStatusCode();
        return (client, (await response.Content.ReadFromJsonAsync<LoginBody>())!);
    }

    private record LoginBody(string AccessToken, string RefreshToken);
}
