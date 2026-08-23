using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;

namespace Talmidon.Tests;

/// <summary>מוודא שתפקיד לא-מורה (הורה/תלמיד, או אנונימי) לא יכול לגעת בנקודות קצה שמורות למורה.</summary>
[Collection(IntegrationTestCollection.Name)]
public class RoleAuthorizationTests(TalmidonWebApplicationFactory factory)
{
    [Fact]
    public async Task AnonymousRequest_ToTeacherOnlyEndpoint_IsRejected()
    {
        var anon = factory.CreateClient();
        var response = await anon.GetAsync("/api/students");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Parent_CannotCreateStudents()
    {
        var (parentClient, _) = await CreateAuthorizedParentAsync();

        var response = await parentClient.PostAsJsonAsync("/api/students", new
        {
            fullName = "ניסיון פריצה",
            gender = (int?)null,
            gradeLevel = (string?)null,
            birthDate = (string?)null,
            generalInfo = (string?)null,
            loginEmail = (string?)null,
            parentIds = Array.Empty<Guid>()
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Parent_CannotSeeTeacherPaymentsEndpoint()
    {
        var (parentClient, _) = await CreateAuthorizedParentAsync();
        var response = await parentClient.GetAsync("/api/payments/open-charges");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Student_CannotSeeAnyPaymentEndpoint()
    {
        var teacher = await TestHelpers.CreateAuthorizedTeacherClientAsync(factory, "roleT");
        var studentEmail = TestHelpers.UniqueEmail("roleStudent");
        var createResponse = await teacher.PostAsJsonAsync("/api/students", new
        {
            fullName = "תלמיד ללא תשלומים",
            gender = (int?)null,
            gradeLevel = (string?)null,
            birthDate = (string?)null,
            generalInfo = (string?)null,
            loginEmail = studentEmail,
            parentIds = Array.Empty<Guid>()
        });
        createResponse.EnsureSuccessStatusCode();

        // מגדירה סיסמה ישירות דרך UserManager (בעקיפת מייל ההזמנה) כדי לקבל טוקן תלמיד תקין
        var studentClient = await LogInAsInvitedUserAsync(studentEmail, "StudentPass123");

        // לתלמיד אין שום נקודת קצה של תשלומים בכלל — גם הגרסה "שלי" חסומה לפי תפקיד
        var response = await studentClient.GetAsync("/api/payments/mine");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private async Task<(HttpClient Client, Guid ParentId)> CreateAuthorizedParentAsync()
    {
        var teacher = await TestHelpers.CreateAuthorizedTeacherClientAsync(factory, "roleParentT");
        var parentEmail = TestHelpers.UniqueEmail("roleParent");
        var createResponse = await teacher.PostAsJsonAsync("/api/parents", new
        {
            fullName = "הורה הרשאות",
            gender = (int?)null,
            email = parentEmail,
            phone = (string?)null
        });
        createResponse.EnsureSuccessStatusCode();
        var parent = await createResponse.Content.ReadFromJsonAsync<ParentDto>();

        var client = await LogInAsInvitedUserAsync(parentEmail, "ParentPass123");
        return (client, parent!.Id);
    }

    /// <summary>קובעת סיסמה ישירות דרך UserManager למשתמש מוזמן (הורה/תלמיד) ומחזירה קליינט מחובר.</summary>
    private async Task<HttpClient> LogInAsInvitedUserAsync(string email, string password)
    {
        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<Talmidon.Infrastructure.Identity.ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(email) ?? throw new InvalidOperationException($"Invited user {email} not found.");
        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        var result = await userManager.ResetPasswordAsync(user, token, password);
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));
        user.EmailConfirmed = true;
        await userManager.UpdateAsync(user);

        var anon = factory.CreateClient();
        var accessToken = await TestHelpers.LoginAsync(anon, email, password);
        return TestHelpers.AuthorizedClient(factory, accessToken);
    }

    private record ParentDto(Guid Id, string FullName);
}
