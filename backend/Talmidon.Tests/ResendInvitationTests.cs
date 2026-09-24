using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Talmidon.Api.Contracts;
using Talmidon.Infrastructure.Identity;

namespace Talmidon.Tests;

/// <summary>
/// The invitation to a parent or a student is sent once, when the teacher
/// creates the account, and a send that fails is only logged. Without a way to
/// send it again, someone who never received it — a failed send, a spam folder,
/// a link that expired — had no way in, and the teacher no way to help.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class ResendInvitationTests(TalmidonWebApplicationFactory factory)
{
    [Fact]
    public async Task ParentList_SaysWhoHasNotAcceptedTheInvitationYet()
    {
        var teacher = await TestHelpers.CreateAuthorizedTeacherClientAsync(factory, "resendList");
        var parent = await CreateParentAsync(teacher, "תמר לוי");

        var before = await GetParentAsync(teacher, parent.Id);
        Assert.False(before.AccountActivated);

        await SetPasswordAsync(parent.Email);

        var after = await GetParentAsync(teacher, parent.Id);
        Assert.True(after.AccountActivated);
    }

    [Fact]
    public async Task ResendInvitation_ForSomeoneWhoNeverSetAPassword_Succeeds()
    {
        var teacher = await TestHelpers.CreateAuthorizedTeacherClientAsync(factory, "resendOk");
        var parent = await CreateParentAsync(teacher, "תמר לוי");

        var response = await teacher.PostAsync($"/api/parents/{parent.Id}/resend-invitation", null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task ResendInvitation_AfterSheHasSetAPassword_IsRefused()
    {
        var teacher = await TestHelpers.CreateAuthorizedTeacherClientAsync(factory, "resendDone");
        var parent = await CreateParentAsync(teacher, "תמר לוי");
        await SetPasswordAsync(parent.Email);

        var response = await teacher.PostAsync($"/api/parents/{parent.Id}/resend-invitation", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    /// <summary>The address belongs to someone real, so another teacher must not be able to poke it.</summary>
    [Fact]
    public async Task ResendInvitation_ForAnotherTeachersParent_IsNotFound()
    {
        var owner = await TestHelpers.CreateAuthorizedTeacherClientAsync(factory, "resendOwner");
        var stranger = await TestHelpers.CreateAuthorizedTeacherClientAsync(factory, "resendStranger");
        var parent = await CreateParentAsync(owner, "תמר לוי");

        var response = await stranger.PostAsync($"/api/parents/{parent.Id}/resend-invitation", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ResendInvitation_ForAStudentWithoutASignIn_IsRefused()
    {
        var teacher = await TestHelpers.CreateAuthorizedTeacherClientAsync(factory, "resendNoLogin");
        var created = await teacher.PostAsJsonAsync("/api/students", new
        {
            fullName = "תלמידה בלי כניסה",
            gender = (int?)null,
            gradeLevel = (string?)null,
            birthDate = (DateOnly?)null,
            generalInfo = (string?)null,
            defaultPricePerLesson = (decimal?)null,
            defaultDurationMinutes = (int?)null,
            loginEmail = (string?)null,
            parentIds = (List<Guid>?)null
        });
        created.EnsureSuccessStatusCode();
        var student = (await created.Content.ReadFromJsonAsync<StudentDetailDto>())!;

        var response = await teacher.PostAsync($"/api/students/{student.Id}/resend-invitation", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    // ----- helpers -----

    private static async Task<ParentDto> CreateParentAsync(HttpClient teacher, string fullName)
    {
        var response = await teacher.PostAsJsonAsync("/api/parents", new
        {
            fullName,
            gender = (int?)null,
            email = TestHelpers.UniqueEmail("invited"),
            phone = "050-1112222"
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ParentDto>())!;
    }

    private static async Task<ParentDto> GetParentAsync(HttpClient teacher, Guid id) =>
        (await teacher.GetFromJsonAsync<ParentDto>($"/api/parents/{id}"))!;

    /// <summary>Walks the invitation link the way she would: set a password, which also verifies the address.</summary>
    private async Task SetPasswordAsync(string email)
    {
        string userId, token;
        using (var scope = factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByEmailAsync(email)
                ?? throw new InvalidOperationException("Invited user not found.");
            userId = user.Id;
            token = Microsoft.AspNetCore.WebUtilities.WebEncoders.Base64UrlEncode(
                System.Text.Encoding.UTF8.GetBytes(await userManager.GeneratePasswordResetTokenAsync(user)));
        }

        var anon = factory.CreateClient();
        var response = await anon.PostAsJsonAsync("/api/auth/set-password",
            new { userId, token, password = "InvitedPass123" });
        response.EnsureSuccessStatusCode();
    }
}
