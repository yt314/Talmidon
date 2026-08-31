using System.Net;
using System.Net.Http.Json;

namespace Talmidon.Tests;

/// <summary>
/// תפקיד-העל למנהל הפלטפורמה: רשימת מורות חוצת-דיירים, נעילה/שחרור של חשבון מורה, ואכיפת
/// שהתפקיד נגיש רק ל-Admin עצמו (לא למורה/אנונימי).
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class AdminTests(TalmidonWebApplicationFactory factory)
{
    [Fact]
    public async Task ListTeachers_ReturnsTeacherAcrossTenants_WithStudentCount()
    {
        var admin = await TestHelpers.CreateAuthorizedAdminClientAsync(factory);

        var email = TestHelpers.UniqueEmail("adminListT");
        const string password = "TestPass123";
        var anon = factory.CreateClient();
        await TestHelpers.RegisterAndConfirmTeacherAsync(factory, anon, email, password, "מורה לספירה");
        var teacherToken = await TestHelpers.LoginAsync(anon, email, password);
        var teacher = TestHelpers.AuthorizedClient(factory, teacherToken);

        var createStudent = await teacher.PostAsJsonAsync("/api/students", new
        {
            fullName = "תלמיד לספירה",
            gender = (int?)null,
            gradeLevel = (string?)null,
            birthDate = (string?)null,
            generalInfo = (string?)null,
            loginEmail = (string?)null,
            parentIds = Array.Empty<Guid>()
        });
        createStudent.EnsureSuccessStatusCode();

        var response = await admin.GetAsync("/api/admin/teachers");
        response.EnsureSuccessStatusCode();
        var teachers = await response.Content.ReadFromJsonAsync<List<AdminTeacherDto>>();

        var found = teachers!.Single(t => t.Email == email);
        Assert.Equal(1, found.StudentCount);
        Assert.False(found.IsLockedOut);
    }

    [Fact]
    public async Task LockTeacher_PreventsLogin_UntilUnlocked()
    {
        var admin = await TestHelpers.CreateAuthorizedAdminClientAsync(factory);

        var email = TestHelpers.UniqueEmail("adminLockT");
        const string password = "TestPass123";
        var anon = factory.CreateClient();
        await TestHelpers.RegisterAndConfirmTeacherAsync(factory, anon, email, password, "מורה לנעילה");

        var teachersResponse = await admin.GetAsync("/api/admin/teachers");
        teachersResponse.EnsureSuccessStatusCode();
        var teachers = await teachersResponse.Content.ReadFromJsonAsync<List<AdminTeacherDto>>();
        var teacherId = teachers!.Single(t => t.Email == email).Id;

        var lockResponse = await admin.PostAsync($"/api/admin/teachers/{teacherId}/lock", null);
        Assert.Equal(HttpStatusCode.NoContent, lockResponse.StatusCode);

        var blockedLogin = await anon.PostAsJsonAsync("/api/auth/login", new { email, password });
        Assert.Equal(HttpStatusCode.Unauthorized, blockedLogin.StatusCode);

        var unlockResponse = await admin.PostAsync($"/api/admin/teachers/{teacherId}/unlock", null);
        Assert.Equal(HttpStatusCode.NoContent, unlockResponse.StatusCode);

        var allowedLogin = await anon.PostAsJsonAsync("/api/auth/login", new { email, password });
        Assert.Equal(HttpStatusCode.OK, allowedLogin.StatusCode);
    }

    [Fact]
    public async Task Teacher_CannotAccessAdminEndpoints()
    {
        var teacher = await TestHelpers.CreateAuthorizedTeacherClientAsync(factory, "notAdminT");
        var response = await teacher.GetAsync("/api/admin/teachers");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AnonymousRequest_ToAdminEndpoint_IsRejected()
    {
        var anon = factory.CreateClient();
        var response = await anon.GetAsync("/api/admin/teachers");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private record AdminTeacherDto(Guid Id, string FullName, string Email, DateTimeOffset CreatedAt, bool IsPublic, int StudentCount, bool IsLockedOut);
}
