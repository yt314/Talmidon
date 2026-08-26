using System.Net;
using System.Net.Http.Json;

namespace Talmidon.Tests;

/// <summary>
/// אסימוני רענון מתחלפים (rotation) בכל שימוש; שימוש חוזר באסימון שכבר הוחלף נחשב חשד לגניבה
/// ושולל את כל משפחת האסימונים של המשתמש — כולל האסימון החדש שכבר הונפק (AuthController.Refresh).
/// זה לא היה מכוסה בכלל: הבדיקות הקיימות בודקות שלילה מפורשת (change-password), לא זיהוי שכפול.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class RefreshTokenReuseTests(TalmidonWebApplicationFactory factory)
{
    [Fact]
    public async Task Refresh_ReusingARotatedToken_RevokesTheEntireFamily()
    {
        var client = factory.CreateClient();
        var email = TestHelpers.UniqueEmail("reuseDetect");
        const string password = "TestPass123";
        await TestHelpers.RegisterAndConfirmTeacherAsync(factory, client, email, password, "מורה לבדיקת שכפול טוקן");

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        loginResponse.EnsureSuccessStatusCode();
        var login = await loginResponse.Content.ReadFromJsonAsync<AuthResponseDto>();

        // רענון ראשון תקין — token1 מוחלף ב-token2.
        var firstRefresh = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = login!.RefreshToken });
        firstRefresh.EnsureSuccessStatusCode();
        var afterFirstRefresh = await firstRefresh.Content.ReadFromJsonAsync<AuthResponseDto>();

        // שימוש חוזר ב-token1 (שכבר הוחלף) = חשד לגניבה -> שולל את כל המשפחה, כולל token2 התקין.
        var reuseAttempt = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = login.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, reuseAttempt.StatusCode);

        var refreshWithToken2 = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = afterFirstRefresh!.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, refreshWithToken2.StatusCode);
    }

    [Fact]
    public async Task Refresh_NormalRotation_IssuesAnAccessTokenThatActuallyWorks()
    {
        var client = factory.CreateClient();
        var email = TestHelpers.UniqueEmail("normalRefresh");
        const string password = "TestPass123";
        await TestHelpers.RegisterAndConfirmTeacherAsync(factory, client, email, password, "מורה רענון רגיל");

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        loginResponse.EnsureSuccessStatusCode();
        var login = await loginResponse.Content.ReadFromJsonAsync<AuthResponseDto>();

        var refreshResponse = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = login!.RefreshToken });
        refreshResponse.EnsureSuccessStatusCode();
        var refreshed = await refreshResponse.Content.ReadFromJsonAsync<AuthResponseDto>();

        var authorized = TestHelpers.AuthorizedClient(factory, refreshed!.AccessToken);
        var response = await authorized.GetAsync("/api/teachers/me");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private record AuthResponseDto(string AccessToken, DateTimeOffset AccessTokenExpiresAt, string RefreshToken, string Email, string[] Roles);
}
