using System.Net;
using System.Net.Http.Json;

namespace Talmidon.Tests;

/// <summary>
/// הבדיקה הכי קריטית באפליקציה הזו: מורה לעולם לא רואה נתונים של מורה אחרת, גם עם
/// טוקן תקין. אם משהו כאן נכשל, זו דליפת מידע פרטי בין מורות — הבאג החמור ביותר האפשרי כאן.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class TenantIsolationTests(TalmidonWebApplicationFactory factory)
{
    [Fact]
    public async Task Teacher_CannotListAnotherTeachersStudents()
    {
        var teacherA = await TestHelpers.CreateAuthorizedTeacherClientAsync(factory, "isoA");
        var teacherB = await TestHelpers.CreateAuthorizedTeacherClientAsync(factory, "isoB");

        var createResponse = await teacherA.PostAsJsonAsync("/api/students", new
        {
            fullName = "תלמידה של מורה A",
            gender = (int?)null,
            gradeLevel = (string?)null,
            birthDate = (string?)null,
            generalInfo = (string?)null,
            loginEmail = (string?)null,
            parentIds = Array.Empty<Guid>()
        });
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<StudentDetailDto>();

        var listAsB = await teacherB.GetFromJsonAsync<List<StudentListItemDto>>("/api/students");
        Assert.DoesNotContain(listAsB!, s => s.Id == created!.Id);

        var listAsA = await teacherA.GetFromJsonAsync<List<StudentListItemDto>>("/api/students");
        Assert.Contains(listAsA!, s => s.Id == created!.Id);
    }

    [Fact]
    public async Task Teacher_CannotFetchAnotherTeachersStudentById()
    {
        var teacherA = await TestHelpers.CreateAuthorizedTeacherClientAsync(factory, "isoA");
        var teacherB = await TestHelpers.CreateAuthorizedTeacherClientAsync(factory, "isoB");

        var created = await CreateStudentAsync(teacherA, "תלמיד פרטי של A");

        // הרישא: לא 200/403 (שהיה חושף קיום) אלא 404 — כאילו התלמיד לא קיים כלל מבחינת מורה B
        var getAsB = await teacherB.GetAsync($"/api/students/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getAsB.StatusCode);
    }

    [Fact]
    public async Task Teacher_CannotDeleteAnotherTeachersStudent()
    {
        var teacherA = await TestHelpers.CreateAuthorizedTeacherClientAsync(factory, "isoA");
        var teacherB = await TestHelpers.CreateAuthorizedTeacherClientAsync(factory, "isoB");
        var created = await CreateStudentAsync(teacherA, "תלמיד שלא נמחק");

        var deleteAsB = await teacherB.DeleteAsync($"/api/students/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, deleteAsB.StatusCode);

        // מוודאים שהתלמיד עדיין קיים אצל המורה האמיתית שלו
        var stillThere = await teacherA.GetAsync($"/api/students/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, stillThere.StatusCode);
    }

    [Fact]
    public async Task Teacher_CannotSeeAnotherTeachersLessons()
    {
        var teacherA = await TestHelpers.CreateAuthorizedTeacherClientAsync(factory, "isoA");
        var teacherB = await TestHelpers.CreateAuthorizedTeacherClientAsync(factory, "isoB");
        var studentA = await CreateStudentAsync(teacherA, "תלמידה עם שיעור");

        var lessonResponse = await teacherA.PostAsJsonAsync("/api/lessons", new
        {
            studentId = studentA.Id,
            startTime = DateTimeOffset.UtcNow.AddDays(1),
            endTime = DateTimeOffset.UtcNow.AddDays(1).AddMinutes(45),
            reason = (string?)null
        });
        lessonResponse.EnsureSuccessStatusCode();

        var lessonsAsB = await teacherB.GetFromJsonAsync<List<LessonDto>>("/api/lessons");
        Assert.Empty(lessonsAsB!);
    }

    [Fact]
    public async Task Teacher_CannotSeeAnotherTeachersNotes()
    {
        var teacherA = await TestHelpers.CreateAuthorizedTeacherClientAsync(factory, "isoA");
        var teacherB = await TestHelpers.CreateAuthorizedTeacherClientAsync(factory, "isoB");
        var studentA = await CreateStudentAsync(teacherA, "תלמידה עם הערה");

        var noteResponse = await teacherA.PostAsJsonAsync("/api/notes", new
        {
            studentId = studentA.Id,
            lessonId = (Guid?)null,
            content = "הערה רגישה של מורה A",
            visibleToStudent = false,
            visibleToParent = false
        });
        noteResponse.EnsureSuccessStatusCode();

        // מורה B מפעילה את אותה נקודת קצה (GET /api/notes ללא studentId) — חייבת לקבל רשימה ריקה, לא את ההערה של A
        var notesAsB = await teacherB.GetFromJsonAsync<List<object>>("/api/notes");
        Assert.Empty(notesAsB!);
    }

    private static async Task<StudentDetailDto> CreateStudentAsync(HttpClient teacherClient, string fullName)
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
        return (await response.Content.ReadFromJsonAsync<StudentDetailDto>())!;
    }

    private record StudentListItemDto(Guid Id, string FullName, string? GradeLevel, bool IsActive, bool HasLogin, int ParentCount);
    private record StudentDetailDto(Guid Id, string FullName);
    private record LessonDto(Guid Id, Guid StudentId, string StudentName);
}
