using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Talmidon.Infrastructure.Identity;

namespace Talmidon.Tests;

/// <summary>מסלול ההרשמה/אימות/התחברות/שחזור סיסמה המלא — כולל שני הבאגים שתוקנו היום (set-password שהיה חסר, forgot-password שלא היה קיים).</summary>
[Collection(IntegrationTestCollection.Name)]
public class AuthFlowTests(TalmidonWebApplicationFactory factory)
{
    [Fact]
    public async Task Login_BeforeEmailConfirmed_IsRejected()
    {
        var client = factory.CreateClient();
        var email = TestHelpers.UniqueEmail("unconfirmed");
        const string password = "TestPass123";

        var register = await client.PostAsJsonAsync("/api/auth/register", new { email, password, fullName = "לא מאומתת", phone = (string?)null });
        register.EnsureSuccessStatusCode();

        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    [Fact]
    public async Task Login_WithWrongPassword_IsRejected()
    {
        var client = factory.CreateClient();
        var email = TestHelpers.UniqueEmail("wrongpw");
        await TestHelpers.RegisterAndConfirmTeacherAsync(factory, client, email, "CorrectPass123", "מורה בדיקה");

        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "TotallyWrongPassword1" });
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    [Fact]
    public async Task Register_WithAlreadyRegisteredEmail_DoesNotRevealExistence()
    {
        var client = factory.CreateClient();
        var email = TestHelpers.UniqueEmail("dup");
        await TestHelpers.RegisterAndConfirmTeacherAsync(factory, client, email, "TestPass123", "מורה כפולה");

        // הרשמה שנייה עם אותו מייל — צריכה להחזיר 200 עם הודעה גנרית זהה, לא לחשוף שהחשבון קיים
        var secondRegister = await client.PostAsJsonAsync("/api/auth/register", new { email, password = "AnotherPass123", fullName = "מורה כפולה 2", phone = (string?)null });
        Assert.Equal(HttpStatusCode.OK, secondRegister.StatusCode);
    }

    [Fact]
    public async Task ConfirmEmail_WithRealToken_ActuallyUnlocksLogin()
    {
        // בניגוד לשאר הבדיקות (שמדלגות על המייל דרך EmailConfirmed=true ישירות), כאן בודקים
        // את הנתיב האמיתי מקצה לקצה: טוקן אמיתי -> GET /api/auth/confirm-email -> login מצליח.
        var client = factory.CreateClient();
        var email = TestHelpers.UniqueEmail("realconfirm");
        const string password = "TestPass123";
        var register = await client.PostAsJsonAsync("/api/auth/register", new { email, password, fullName = "אימות אמיתי", phone = (string?)null });
        register.EnsureSuccessStatusCode();

        string confirmUrl;
        using (var scope = factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByEmailAsync(email) ?? throw new InvalidOperationException("User not found.");
            var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
            var encoded = WebEncoders.Base64UrlEncode(System.Text.Encoding.UTF8.GetBytes(token));
            confirmUrl = $"/api/auth/confirm-email?userId={user.Id}&token={encoded}";
        }

        using var noRedirectClient = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var confirmResponse = await noRedirectClient.GetAsync(confirmUrl);
        Assert.Equal(HttpStatusCode.Redirect, confirmResponse.StatusCode);
        Assert.Contains("confirmed=1", confirmResponse.Headers.Location?.ToString());

        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    [Fact]
    public async Task ForgotPassword_ThenNewPassword_LogsInAndOldPasswordFails()
    {
        var client = factory.CreateClient();
        var email = TestHelpers.UniqueEmail("forgot");
        const string oldPassword = "OldPass123";
        const string newPassword = "NewPass456";
        await TestHelpers.RegisterAndConfirmTeacherAsync(factory, client, email, oldPassword, "מורה שכחנית");

        var forgot = await client.PostAsJsonAsync("/api/auth/forgot-password", new { email });
        Assert.Equal(HttpStatusCode.OK, forgot.StatusCode);

        string resetToken;
        string userId;
        using (var scope = factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByEmailAsync(email) ?? throw new InvalidOperationException("User not found.");
            userId = user.Id;
            var token = await userManager.GeneratePasswordResetTokenAsync(user);
            resetToken = WebEncoders.Base64UrlEncode(System.Text.Encoding.UTF8.GetBytes(token));
        }

        var setPassword = await client.PostAsJsonAsync("/api/auth/set-password", new { userId, token = resetToken, password = newPassword });
        Assert.Equal(HttpStatusCode.OK, setPassword.StatusCode);

        var loginWithNew = await client.PostAsJsonAsync("/api/auth/login", new { email, password = newPassword });
        Assert.Equal(HttpStatusCode.OK, loginWithNew.StatusCode);

        var loginWithOld = await client.PostAsJsonAsync("/api/auth/login", new { email, password = oldPassword });
        Assert.Equal(HttpStatusCode.Unauthorized, loginWithOld.StatusCode);
    }

    [Fact]
    public async Task ChangePassword_WithWrongCurrentPassword_ReturnsHebrewError()
    {
        var client = factory.CreateClient();
        var email = TestHelpers.UniqueEmail("changepw");
        const string password = "TestPass123";
        await TestHelpers.RegisterAndConfirmTeacherAsync(factory, client, email, password, "מורה משנה סיסמה");
        var token = await TestHelpers.LoginAsync(client, email, password);
        var authorized = TestHelpers.AuthorizedClient(factory, token);

        var response = await authorized.PostAsJsonAsync("/api/auth/change-password", new
        {
            currentPassword = "TotallyWrongPassword1",
            newPassword = "SomeNewPass456"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("הסיסמה הנוכחית שגויה", body);
    }

    [Fact]
    public async Task ChangePassword_RevokesRefreshToken_SoOldSessionCannotRefresh()
    {
        var client = factory.CreateClient();
        var email = TestHelpers.UniqueEmail("revoke");
        const string oldPassword = "OldRevokePass123";
        await TestHelpers.RegisterAndConfirmTeacherAsync(factory, client, email, oldPassword, "מורה מתנתקת");

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new { email, password = oldPassword });
        loginResponse.EnsureSuccessStatusCode();
        var login = await loginResponse.Content.ReadFromJsonAsync<LoginResultDto>();
        var authorized = TestHelpers.AuthorizedClient(factory, login!.AccessToken);

        var changeResponse = await authorized.PostAsJsonAsync("/api/auth/change-password", new
        {
            currentPassword = oldPassword,
            newPassword = "BrandNewPass456"
        });
        changeResponse.EnsureSuccessStatusCode();

        var refreshResponse = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = login.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, refreshResponse.StatusCode);
    }

    private record LoginResultDto(string AccessToken, DateTimeOffset AccessTokenExpiresAt, string RefreshToken, string Email, string[] Roles);
}
