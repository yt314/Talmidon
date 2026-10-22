using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Talmidon.Infrastructure.Identity;

namespace Talmidon.Tests;

/// <summary>
/// ניהול תלמידים (StudentsController) — מה שלא היה מכוסה: יצירה עם כניסה משלה ועם הורים
/// (כולל הורה של מורה אחרת, שחייב להיות מסונן), עדכון, מחיקה שמוחקת גם את חשבון הכניסה,
/// קישור/ניתוק הורה, ושני המסכים של המשפחה: "הילדים שלי" ו"הפרופיל שלי".
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class StudentsTests(TalmidonWebApplicationFactory factory)
{
    private const string Password = "FamilyPass123";

    [Fact]
    public async Task Create_WithALoginEmail_InvitesTheStudent_AndLinksTheGivenParent()
    {
        var teacher = await TestHelpers.CreateAuthorizedTeacherClientAsync(factory, "stuCreateT");
        var parent = await CreateParentAsync(teacher, "stuCreateParent");
        var loginEmail = TestHelpers.UniqueEmail("stuCreateKid");

        var created = await CreateStudentAsync(teacher, "נועה", loginEmail, parent.Id);

        Assert.True(created.HasLogin);
        Assert.Equal(parent.Id, Assert.Single(created.Parents).Id);
        Assert.Single(factory.SentEmails.To(loginEmail));
    }

    [Fact]
    public async Task Create_IgnoresAParentThatBelongsToAnotherTeacher()
    {
        var teacherA = await TestHelpers.CreateAuthorizedTeacherClientAsync(factory, "stuForeignA");
        var teacherB = await TestHelpers.CreateAuthorizedTeacherClientAsync(factory, "stuForeignB");
        var foreignParent = await CreateParentAsync(teacherB, "stuForeignParent");

        var created = await CreateStudentAsync(teacherA, "יעל", loginEmail: null, foreignParent.Id);

        Assert.Empty(created.Parents);
    }

    [Fact]
    public async Task Update_ChangesTheStudentsDetails()
    {
        var teacher = await TestHelpers.CreateAuthorizedTeacherClientAsync(factory, "stuUpdateT");
        var created = await CreateStudentAsync(teacher, "דנה", loginEmail: null);

        var update = await teacher.PutAsJsonAsync($"/api/students/{created.Id}", new
        {
            fullName = "דנה כהן",
            gender = (int?)null,
            gradeLevel = "ה׳",
            birthDate = (string?)null,
            generalInfo = (string?)null,
            defaultPricePerLesson = 130m,
            defaultDurationMinutes = 45,
            isActive = false
        });
        Assert.Equal(HttpStatusCode.NoContent, update.StatusCode);

        var detail = await teacher.GetFromJsonAsync<StudentDto>($"/api/students/{created.Id}");
        Assert.Equal(("דנה כהן", "ה׳", 130m, 45, false),
            (detail!.FullName, detail.GradeLevel, detail.DefaultPricePerLesson, detail.DefaultDurationMinutes, detail.IsActive));
    }

    [Fact]
    public async Task Delete_RemovesTheStudentAndTheirLoginAccount()
    {
        var teacher = await TestHelpers.CreateAuthorizedTeacherClientAsync(factory, "stuDeleteT");
        var loginEmail = TestHelpers.UniqueEmail("stuDeleteKid");
        var created = await CreateStudentAsync(teacher, "מיכל", loginEmail);

        var delete = await teacher.DeleteAsync($"/api/students/{created.Id}");

        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await teacher.GetAsync($"/api/students/{created.Id}")).StatusCode);
        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        Assert.Null(await userManager.FindByEmailAsync(loginEmail));
    }

    [Fact]
    public async Task LinkIsIdempotent_AndUnlinkRemovesTheParent()
    {
        var teacher = await TestHelpers.CreateAuthorizedTeacherClientAsync(factory, "stuLinkT");
        var parent = await CreateParentAsync(teacher, "stuLinkParent");
        var created = await CreateStudentAsync(teacher, "שירה", loginEmail: null);
        var link = $"/api/students/{created.Id}/parents/{parent.Id}";

        Assert.Equal(HttpStatusCode.NoContent, (await teacher.PostAsync(link, null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await teacher.PostAsync(link, null)).StatusCode);
        Assert.Single((await teacher.GetFromJsonAsync<StudentDto>($"/api/students/{created.Id}"))!.Parents);

        Assert.Equal(HttpStatusCode.NoContent, (await teacher.DeleteAsync(link)).StatusCode);
        Assert.Empty((await teacher.GetFromJsonAsync<StudentDto>($"/api/students/{created.Id}"))!.Parents);
        Assert.Equal(HttpStatusCode.NotFound, (await teacher.DeleteAsync(link)).StatusCode);
    }

    [Fact]
    public async Task ParentSeesOnlyTheirOwnChildren_AndStudentSeesTheirOwnProfile()
    {
        var teacher = await TestHelpers.CreateAuthorizedTeacherClientAsync(factory, "stuFamilyT");
        var parent = await CreateParentAsync(teacher, "stuFamilyParent");
        var kidEmail = TestHelpers.UniqueEmail("stuFamilyKid");
        var own = await CreateStudentAsync(teacher, "הבת שלי", kidEmail, parent.Id);
        await CreateStudentAsync(teacher, "ילדה אחרת", loginEmail: null);

        var parentClient = await ActivateAndLoginAsync(parent.Email);
        var children = await parentClient.GetFromJsonAsync<List<ChildDto>>("/api/students/mine");
        Assert.Equal(own.Id, Assert.Single(children!).Id);

        var studentClient = await ActivateAndLoginAsync(kidEmail);
        var me = await studentClient.GetFromJsonAsync<ProfileDto>("/api/students/me");
        Assert.Equal("הבת שלי", me!.FullName);
    }

    // ----- עזר -----

    private static async Task<ParentDto> CreateParentAsync(HttpClient teacher, string fullName)
    {
        var response = await teacher.PostAsJsonAsync("/api/parents", new
        {
            fullName,
            gender = (int?)null,
            email = TestHelpers.UniqueEmail(fullName),
            phone = (string?)null
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ParentDto>())!;
    }

    private static async Task<StudentDto> CreateStudentAsync(HttpClient teacher, string fullName, string? loginEmail, params Guid[] parentIds)
    {
        var response = await teacher.PostAsJsonAsync("/api/students", new
        {
            fullName,
            gender = (int?)null,
            gradeLevel = (string?)null,
            birthDate = (string?)null,
            generalInfo = (string?)null,
            loginEmail,
            parentIds
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<StudentDto>())!;
    }

    /// <summary>במקום קישור ההזמנה: קובעת סיסמה ישירות ומתחברת.</summary>
    private async Task<HttpClient> ActivateAndLoginAsync(string email)
    {
        using (var scope = factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByEmailAsync(email) ?? throw new InvalidOperationException($"{email} not found.");
            var token = await userManager.GeneratePasswordResetTokenAsync(user);
            var result = await userManager.ResetPasswordAsync(user, token, Password);
            if (!result.Succeeded) throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));
            user.EmailConfirmed = true;
            await userManager.UpdateAsync(user);
        }
        var accessToken = await TestHelpers.LoginAsync(factory.CreateClient(), email, Password);
        return TestHelpers.AuthorizedClient(factory, accessToken);
    }

    private record ParentDto(Guid Id, string FullName, string Email);
    private record ParentSummary(Guid Id);
    private record StudentDto(
        Guid Id, string FullName, string? GradeLevel, decimal? DefaultPricePerLesson, int? DefaultDurationMinutes,
        bool IsActive, bool HasLogin, List<ParentSummary> Parents);
    private record ChildDto(Guid Id, string FullName);
    private record ProfileDto(string FullName);
}
