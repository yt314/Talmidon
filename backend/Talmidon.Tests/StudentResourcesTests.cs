using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Talmidon.Infrastructure.Identity;

namespace Talmidon.Tests;

/// <summary>
/// חומרי לימוד. בשונה מהערה פדגוגית, חומר לימוד הוא משותף מעצם הגדרתו — אין לו דגלי
/// נראות, וכל חומר שהמורה מוסיפה לתלמיד אמור להגיע לתלמיד ולהוריו. לכן הבדיקות כאן
/// מתמקדות בגבול השני: שהוא מגיע *רק* אליהם — לא לתלמיד אחר של אותה מורה, לא להורה
/// של ילד אחר, ובוודאי לא למורה אחרת.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class StudentResourcesTests(TalmidonWebApplicationFactory factory)
{
    [Fact]
    public async Task StudentSeesTheResourcesTheirTeacherAddedForThem()
    {
        var teacher = await TestHelpers.CreateAuthorizedTeacherClientAsync(factory, "resTeacher");
        var (studentClient, studentId) = await CreateStudentWithLoginAsync(teacher, "resStudent");
        var resourceId = await CreateResourceAsync(teacher, studentId, "דף תרגול שברים");

        var mine = await studentClient.GetFromJsonAsync<List<PortalResourceDto>>("/api/resources/my-resources");

        Assert.Contains(mine!, r => r.Id == resourceId && r.Title == "דף תרגול שברים");
    }

    [Fact]
    public async Task MyResources_OnlyReturnsTheAuthenticatedStudentsOwnResources()
    {
        var teacher = await TestHelpers.CreateAuthorizedTeacherClientAsync(factory, "resIdorTeacher");
        var (studentAClient, studentAId) = await CreateStudentWithLoginAsync(teacher, "resStudentA");
        var (_, studentBId) = await CreateStudentWithLoginAsync(teacher, "resStudentB");

        var resourceAId = await CreateResourceAsync(teacher, studentAId, "חומר של א");
        var resourceBId = await CreateResourceAsync(teacher, studentBId, "חומר של ב");

        var mine = await studentAClient.GetFromJsonAsync<List<PortalResourceDto>>("/api/resources/my-resources");

        Assert.Contains(mine!, r => r.Id == resourceAId);
        Assert.DoesNotContain(mine!, r => r.Id == resourceBId);
    }

    [Fact]
    public async Task ParentSeesTheirOwnChildsResourcesButNotAnotherParentsChilds()
    {
        var teacher = await TestHelpers.CreateAuthorizedTeacherClientAsync(factory, "resParentTeacher");
        var (parentAClient, _, childAId) = await CreateParentWithChildAsync(teacher, "הורה א", "ילד א");
        var (_, _, childBId) = await CreateParentWithChildAsync(teacher, "הורה ב", "ילד ב");

        var resourceAId = await CreateResourceAsync(teacher, childAId, "חומר לילד א");
        var resourceBId = await CreateResourceAsync(teacher, childBId, "חומר לילד ב");

        var mine = await parentAClient.GetFromJsonAsync<List<PortalResourceDto>>("/api/resources/mine");

        Assert.Contains(mine!, r => r.Id == resourceAId);
        Assert.DoesNotContain(mine!, r => r.Id == resourceBId);
    }

    [Fact]
    public async Task ParentCanFilterResourcesToASingleChild()
    {
        var teacher = await TestHelpers.CreateAuthorizedTeacherClientAsync(factory, "resFilterTeacher");
        var (parentClient, parentId, firstChildId) = await CreateParentWithChildAsync(teacher, "הורה מסנן", "ילד ראשון");

        var secondChildId = await CreateStudentForExistingParentAsync(teacher, parentId, "ילד שני");

        var firstResourceId = await CreateResourceAsync(teacher, firstChildId, "חומר לילד ראשון");
        var secondResourceId = await CreateResourceAsync(teacher, secondChildId, "חומר לילד שני");

        var filtered = await parentClient.GetFromJsonAsync<List<PortalResourceDto>>(
            $"/api/resources/mine?studentId={firstChildId}");

        Assert.Contains(filtered!, r => r.Id == firstResourceId);
        Assert.DoesNotContain(filtered!, r => r.Id == secondResourceId);
    }

    [Fact]
    public async Task CreatingAResourceWithANonHttpUrlIsRejected()
    {
        var teacher = await TestHelpers.CreateAuthorizedTeacherClientAsync(factory, "resUrlTeacher");
        var studentId = await CreateStudentAsync(teacher, "תלמיד קישורים");

        // הקישור מוצג לתלמיד ולהורה כ-href לחיץ, ולכן חייב להיחסם — בין אם ע"י ‎[Url]‎
        // ובין אם ע"י בדיקת הסכימה בבקר. הבדיקה כאן על ההתנהגות, לא על מי חסם.
        var response = await teacher.PostAsJsonAsync($"/api/students/{studentId}/resources", new
        {
            title = "קישור זדוני",
            url = "javascript:alert(1)",
            description = (string?)null
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AnotherTeacherCanNeitherListNorDeleteResourcesOfThisTeachersStudent()
    {
        var teacherA = await TestHelpers.CreateAuthorizedTeacherClientAsync(factory, "resTenantA");
        var teacherB = await TestHelpers.CreateAuthorizedTeacherClientAsync(factory, "resTenantB");

        var studentId = await CreateStudentAsync(teacherA, "תלמיד של מורה א");
        var resourceId = await CreateResourceAsync(teacherA, studentId, "חומר של מורה א");

        var listedByB = await teacherB.GetFromJsonAsync<List<StudentResourceDto>>($"/api/students/{studentId}/resources");
        Assert.Empty(listedByB!);

        var deleteByB = await teacherB.DeleteAsync($"/api/students/{studentId}/resources/{resourceId}");
        Assert.Equal(HttpStatusCode.NotFound, deleteByB.StatusCode);

        // ולוודא שהחומר עדיין שם אצל הבעלים
        var listedByA = await teacherA.GetFromJsonAsync<List<StudentResourceDto>>($"/api/students/{studentId}/resources");
        Assert.Contains(listedByA!, r => r.Id == resourceId);
    }

    [Fact]
    public async Task DeletingAResourceRemovesItFromTheStudentsPortal()
    {
        var teacher = await TestHelpers.CreateAuthorizedTeacherClientAsync(factory, "resDeleteTeacher");
        var (studentClient, studentId) = await CreateStudentWithLoginAsync(teacher, "resDeleteStudent");
        var resourceId = await CreateResourceAsync(teacher, studentId, "חומר שיימחק");

        var delete = await teacher.DeleteAsync($"/api/students/{studentId}/resources/{resourceId}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        var mine = await studentClient.GetFromJsonAsync<List<PortalResourceDto>>("/api/resources/my-resources");
        Assert.DoesNotContain(mine!, r => r.Id == resourceId);
    }

    // ----- עזר -----

    private static async Task<Guid> CreateResourceAsync(HttpClient teacherClient, Guid studentId, string title)
    {
        var response = await teacherClient.PostAsJsonAsync($"/api/students/{studentId}/resources", new
        {
            title,
            url = "https://example.com/material",
            description = (string?)null
        });
        response.EnsureSuccessStatusCode();
        var resource = await response.Content.ReadFromJsonAsync<StudentResourceDto>();
        return resource!.Id;
    }

    private static async Task<Guid> CreateStudentAsync(HttpClient teacherClient, string fullName)
    {
        var response = await teacherClient.PostAsJsonAsync("/api/students", new
        {
            fullName,
            gender = (int?)null,
            gradeLevel = (string?)null,
            birthDate = (string?)null,
            generalInfo = (string?)null,
            loginEmail = (string?)null,
            parentIds = Array.Empty<Guid>()
        });
        response.EnsureSuccessStatusCode();
        var student = await response.Content.ReadFromJsonAsync<StudentDto>();
        return student!.Id;
    }

    /// <summary>ילד נוסף להורה שכבר קיים.</summary>
    private static async Task<Guid> CreateStudentForExistingParentAsync(
        HttpClient teacherClient, Guid parentId, string fullName)
    {
        var response = await teacherClient.PostAsJsonAsync("/api/students", new
        {
            fullName,
            gender = (int?)null,
            gradeLevel = (string?)null,
            birthDate = (string?)null,
            generalInfo = (string?)null,
            loginEmail = (string?)null,
            parentIds = new[] { parentId }
        });
        response.EnsureSuccessStatusCode();
        var student = await response.Content.ReadFromJsonAsync<StudentDto>();
        return student!.Id;
    }

    private async Task<(HttpClient StudentClient, Guid StudentId)> CreateStudentWithLoginAsync(
        HttpClient teacherClient, string prefix)
    {
        var studentEmail = TestHelpers.UniqueEmail(prefix);
        var studentResponse = await teacherClient.PostAsJsonAsync("/api/students", new
        {
            fullName = $"תלמיד/ה {prefix}",
            gender = (int?)null,
            gradeLevel = (string?)null,
            birthDate = (string?)null,
            generalInfo = (string?)null,
            loginEmail = studentEmail,
            parentIds = Array.Empty<Guid>()
        });
        studentResponse.EnsureSuccessStatusCode();
        var student = await studentResponse.Content.ReadFromJsonAsync<StudentDto>();

        var studentClient = await SetPasswordAndLoginAsync(studentEmail, "StudentPass123");
        return (studentClient, student!.Id);
    }

    private async Task<(HttpClient ParentClient, Guid ParentId, Guid StudentId)> CreateParentWithChildAsync(
        HttpClient teacherClient, string parentName, string studentName)
    {
        var parentEmail = TestHelpers.UniqueEmail("resParent");
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

        var parentClient = await SetPasswordAndLoginAsync(parentEmail, "ParentPass123");
        return (parentClient, parent!.Id, student!.Id);
    }

    /// <summary>
    /// חשבונות הורה/תלמיד נוצרים ע"י המורה בלי סיסמה (הם מקבלים מייל הזמנה). בבדיקות
    /// קובעים להם סיסמה ישירות דרך UserManager, כמו בשאר קבצי הבדיקות.
    /// </summary>
    private async Task<HttpClient> SetPasswordAndLoginAsync(string email, string password)
    {
        using (var scope = factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByEmailAsync(email)
                ?? throw new InvalidOperationException($"User {email} not found.");
            var token = await userManager.GeneratePasswordResetTokenAsync(user);
            var result = await userManager.ResetPasswordAsync(user, token, password);
            if (!result.Succeeded)
                throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));
            user.EmailConfirmed = true;
            await userManager.UpdateAsync(user);
        }

        var anon = factory.CreateClient();
        var accessToken = await TestHelpers.LoginAsync(anon, email, password);
        return TestHelpers.AuthorizedClient(factory, accessToken);
    }

    private record ParentDto(Guid Id, string FullName);
    private record StudentDto(Guid Id, string FullName);
    private record StudentResourceDto(Guid Id, Guid StudentId, string Title, string Url, string? Description, DateTimeOffset CreatedAt);
    private record PortalResourceDto(
        Guid Id, Guid StudentId, string StudentName, string Title, string Url, string? Description, DateTimeOffset CreatedAt);
}
