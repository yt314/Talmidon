using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Talmidon.Infrastructure.Identity;

namespace Talmidon.Tests;

/// <summary>
/// שני הורים שונים תחת אותה מורה (אותו דייר) — מוודא שהורה אחד לא יכול לבקש/לשנות שיעורים
/// של ילד ששייך להורה השני, למרות שהם "בפנים" אותו דייר ונכשלים בלי לחשוף מידע (403/404).
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class ParentIdorTests(TalmidonWebApplicationFactory factory)
{
    [Fact]
    public async Task Parent_CannotRequestLessonForAnotherParentsChild()
    {
        var teacher = await TestHelpers.CreateAuthorizedTeacherClientAsync(factory, "idorT");
        var (parentAClient, _) = await CreateParentWithChildAsync(teacher, "הורה A", "ילד של A");
        var (_, studentBId) = await CreateParentWithChildAsync(teacher, "הורה B", "ילד של B");

        var response = await parentAClient.PostAsJsonAsync("/api/lessons/requests", new
        {
            studentId = studentBId,
            startTime = DateTimeOffset.UtcNow.AddDays(1),
            endTime = DateTimeOffset.UtcNow.AddDays(1).AddMinutes(45),
            reason = (string?)null
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Parent_CannotRequestChangeToAnotherParentsChildsLesson()
    {
        var teacher = await TestHelpers.CreateAuthorizedTeacherClientAsync(factory, "idorT2");
        var (_, studentAId) = await CreateParentWithChildAsync(teacher, "הורה C", "ילד של C");
        var (parentDClient, _) = await CreateParentWithChildAsync(teacher, "הורה D", "ילד של D");

        // המורה קובעת שיעור מתוזמן לילד של הורה C
        var lessonResponse = await teacher.PostAsJsonAsync("/api/lessons", new
        {
            studentId = studentAId,
            startTime = DateTimeOffset.UtcNow.AddDays(2),
            endTime = DateTimeOffset.UtcNow.AddDays(2).AddMinutes(45),
            reason = (string?)null
        });
        lessonResponse.EnsureSuccessStatusCode();
        var lesson = await lessonResponse.Content.ReadFromJsonAsync<LessonDto>();

        // הורה D (לא ההורה של הילד הזה) מנסה לבקש ביטול לשיעור הזה
        var response = await parentDClient.PostAsJsonAsync($"/api/lessons/{lesson!.Id}/change-requests", new
        {
            type = 0, // Cancel
            proposedStartTime = (DateTimeOffset?)null,
            proposedEndTime = (DateTimeOffset?)null,
            reason = "ניסיון לבטל שיעור של ילד אחר"
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private async Task<(HttpClient ParentClient, Guid StudentId)> CreateParentWithChildAsync(
        HttpClient teacherClient, string parentName, string studentName)
    {
        var parentEmail = TestHelpers.UniqueEmail("idorParent");
        var parentResponse = await teacherClient.PostAsJsonAsync("/api/parents", new
        {
            fullName = parentName,
            gender = (int?)null,
            email = parentEmail,
            phone = (string?)null
        });
        parentResponse.EnsureSuccessStatusCode();
        var parent = await parentResponse.Content.ReadFromJsonAsync<ParentDto>();

        var studentResponse = await teacherClient.PostAsJsonAsync("/api/students", new
        {
            fullName = studentName,
            gender = (int?)null,
            gradeLevel = (string?)null,
            birthDate = (string?)null,
            generalInfo = (string?)null,
            loginEmail = (string?)null,
            parentIds = new[] { parent!.Id }
        });
        studentResponse.EnsureSuccessStatusCode();
        var student = await studentResponse.Content.ReadFromJsonAsync<StudentDto>();

        const string password = "ParentPass123";
        using (var scope = factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByEmailAsync(parentEmail) ?? throw new InvalidOperationException("Parent user not found.");
            var token = await userManager.GeneratePasswordResetTokenAsync(user);
            var result = await userManager.ResetPasswordAsync(user, token, password);
            if (!result.Succeeded)
                throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));
            user.EmailConfirmed = true;
            await userManager.UpdateAsync(user);
        }

        var anon = factory.CreateClient();
        var accessToken = await TestHelpers.LoginAsync(anon, parentEmail, password);
        var parentClient = TestHelpers.AuthorizedClient(factory, accessToken);

        return (parentClient, student!.Id);
    }

    private record ParentDto(Guid Id, string FullName);
    private record StudentDto(Guid Id, string FullName);
    private record LessonDto(Guid Id);
}
