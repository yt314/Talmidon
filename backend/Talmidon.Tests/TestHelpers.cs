using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Talmidon.Infrastructure.Auth;
using Talmidon.Infrastructure.Identity;

namespace Talmidon.Tests;

/// <summary>פעולות חוזרות לבדיקות: הרשמה+אימות ישיר (בעקיפת Mailpit), התחברות, ולקוח HTTP עם Bearer.</summary>
public static class TestHelpers
{
    public static string UniqueEmail(string prefix) => $"{prefix}-{Guid.NewGuid():N}@example.com";

    /// <summary>נרשמת כמורה ומאמתת את המייל ישירות דרך UserManager (לא דרך קישור מייל אמיתי — Mailpit לא רץ בבדיקות).</summary>
    public static async Task RegisterAndConfirmTeacherAsync(
        TalmidonWebApplicationFactory factory, HttpClient client, string email, string password, string fullName)
    {
        var register = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password,
            fullName,
            phone = (string?)null
        });
        register.EnsureSuccessStatusCode();

        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(email)
            ?? throw new InvalidOperationException($"User {email} not found right after registration.");
        user.EmailConfirmed = true;
        await userManager.UpdateAsync(user);
    }

    public static async Task<string> LoginAsync(HttpClient client, string email, string password)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<AuthResponseDto>();
        return body!.AccessToken;
    }

    public static HttpClient AuthorizedClient(TalmidonWebApplicationFactory factory, string accessToken)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }

    /// <summary>יוצרת מורה מאומתת ומחזירה קליינט HTTP מחובר כמותה — הבניין הבסיסי ביותר לרוב הבדיקות.</summary>
    public static async Task<HttpClient> CreateAuthorizedTeacherClientAsync(
        TalmidonWebApplicationFactory factory, string emailPrefix = "teacher")
    {
        var anon = factory.CreateClient();
        var email = UniqueEmail(emailPrefix);
        const string password = "TestPass123";
        await RegisterAndConfirmTeacherAsync(factory, anon, email, password, "מורה בדיקה");
        var token = await LoginAsync(anon, email, password);
        return AuthorizedClient(factory, token);
    }

    /// <summary>
    /// יוצרת חשבון מנהל-מערכת ומחזירה קליינט HTTP מחובר כמותו. בניגוד למורה, אין הרשמה עצמית
    /// למנהל — נוצר ישירות דרך UserManager, כמו שהזריעה האמיתית ב-Program.cs עושה.
    /// </summary>
    public static async Task<HttpClient> CreateAuthorizedAdminClientAsync(TalmidonWebApplicationFactory factory)
    {
        var email = UniqueEmail("admin");
        const string password = "AdminTestPass123!";

        using (var scope = factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var admin = new ApplicationUser { UserName = email, Email = email, DisplayName = "מנהל בדיקה", EmailConfirmed = true };
            var createResult = await userManager.CreateAsync(admin, password);
            if (!createResult.Succeeded)
                throw new InvalidOperationException(string.Join("; ", createResult.Errors.Select(e => e.Description)));
            await userManager.AddToRoleAsync(admin, Roles.Admin);
        }

        var anon = factory.CreateClient();
        var token = await LoginAsync(anon, email, password);
        return AuthorizedClient(factory, token);
    }

    private record AuthResponseDto(string AccessToken, DateTimeOffset AccessTokenExpiresAt, string RefreshToken, string Email, string[] Roles);
}
