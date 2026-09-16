using System.Net.Http.Json;
using Talmidon.Api.Contracts;

namespace Talmidon.Tests;

/// <summary>
/// טבלת "מי חייב כמה". הסיכון האמיתי כאן הוא ספירה כפולה: תלמיד המשויך לשני הורים
/// היה מופיע פעמיים ומנפח את סך החוב, ולכן הוא נספר אצל הורה אחד בלבד.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class OpenChargesSummaryTests(TalmidonWebApplicationFactory factory)
{
    [Fact]
    public async Task Summary_GroupsOpenChargesByParent()
    {
        var teacher = await TestHelpers.CreateAuthorizedTeacherClientAsync(factory, "summaryByParent");
        var studentId = await CreateStudentAsync(teacher, "תלמיד סיכום");
        var parentId = await LinkParentAsync(teacher, studentId, "הורה סיכום");
        await CreateChargeAsync(teacher, studentId, 120m, DateTimeOffset.UtcNow.AddDays(-3));
        await CreateChargeAsync(teacher, studentId, 80m, DateTimeOffset.UtcNow.AddDays(-1));

        var summary = await teacher.GetFromJsonAsync<List<OpenChargeSummaryDto>>("/api/payments/open-charges/summary");

        var row = Assert.Single(summary!);
        Assert.Equal(parentId, row.ParentId);
        Assert.Equal(2, row.LessonCount);
        Assert.Equal(1, row.StudentCount);
        Assert.Equal(200m, row.Total);
    }

    [Fact]
    public async Task Summary_CountsAStudentWithTwoParentsOnce()
    {
        var teacher = await TestHelpers.CreateAuthorizedTeacherClientAsync(factory, "summaryTwoParents");
        var studentId = await CreateStudentAsync(teacher, "תלמיד שני הורים");
        await LinkParentAsync(teacher, studentId, "אב");
        await LinkParentAsync(teacher, studentId, "בת");
        await CreateChargeAsync(teacher, studentId, 150m, DateTimeOffset.UtcNow.AddDays(-2));

        var summary = await teacher.GetFromJsonAsync<List<OpenChargeSummaryDto>>("/api/payments/open-charges/summary");

        var row = Assert.Single(summary!);
        Assert.Equal(1, row.LessonCount);
        Assert.Equal(150m, row.Total);
    }

    [Fact]
    public async Task Summary_ShowsChargesOfAStudentWithNoParentInsteadOfHidingThem()
    {
        var teacher = await TestHelpers.CreateAuthorizedTeacherClientAsync(factory, "summaryNoParent");
        var studentId = await CreateStudentAsync(teacher, "תלמיד ללא הורה");
        await CreateChargeAsync(teacher, studentId, 90m, DateTimeOffset.UtcNow.AddDays(-5));

        var summary = await teacher.GetFromJsonAsync<List<OpenChargeSummaryDto>>("/api/payments/open-charges/summary");

        var row = Assert.Single(summary!);
        Assert.Null(row.ParentId);
        Assert.Equal(90m, row.Total);
    }

    [Fact]
    public async Task Summary_IgnoresLessonsThatWereNotCharged()
    {
        var teacher = await TestHelpers.CreateAuthorizedTeacherClientAsync(factory, "summaryFreeLesson");
        var studentId = await CreateStudentAsync(teacher, "תלמיד ניסיון");
        await LinkParentAsync(teacher, studentId, "הורה ניסיון");
        var lessonId = await CreateLessonAsync(teacher, studentId, DateTimeOffset.UtcNow.AddDays(-1));
        var complete = await teacher.PostAsJsonAsync($"/api/lessons/{lessonId}/complete", new
        {
            completed = true,
            paymentRequired = false,
            amount = 0m,
            noteVisibleToStudent = false,
            noteVisibleToParent = false
        });
        complete.EnsureSuccessStatusCode();

        var summary = await teacher.GetFromJsonAsync<List<OpenChargeSummaryDto>>("/api/payments/open-charges/summary");

        Assert.Empty(summary!);
    }

    private static async Task<Guid> CreateStudentAsync(HttpClient teacher, string fullName)
    {
        var response = await teacher.PostAsJsonAsync("/api/students", new
        {
            fullName,
            defaultPricePerLesson = 100m,
            defaultDurationMinutes = 60
        });
        response.EnsureSuccessStatusCode();
        var student = await response.Content.ReadFromJsonAsync<StudentDetailDto>();
        return student!.Id;
    }

    private static async Task<Guid> LinkParentAsync(HttpClient teacher, Guid studentId, string fullName)
    {
        var created = await teacher.PostAsJsonAsync("/api/parents", new
        {
            fullName,
            email = TestHelpers.UniqueEmail("summaryParent"),
            phone = (string?)null
        });
        created.EnsureSuccessStatusCode();
        var parent = await created.Content.ReadFromJsonAsync<ParentDto>();

        var link = await teacher.PostAsync($"/api/students/{studentId}/parents/{parent!.Id}", null);
        link.EnsureSuccessStatusCode();
        return parent.Id;
    }

    private static async Task<Guid> CreateLessonAsync(HttpClient teacher, Guid studentId, DateTimeOffset start)
    {
        var response = await teacher.PostAsJsonAsync("/api/lessons", new
        {
            studentId,
            startTime = start,
            endTime = start.AddMinutes(60)
        });
        response.EnsureSuccessStatusCode();
        var lesson = await response.Content.ReadFromJsonAsync<LessonDto>();
        return lesson!.Id;
    }

    private static async Task CreateChargeAsync(HttpClient teacher, Guid studentId, decimal amount, DateTimeOffset start)
    {
        var lessonId = await CreateLessonAsync(teacher, studentId, start);
        var response = await teacher.PostAsJsonAsync($"/api/lessons/{lessonId}/complete", new
        {
            completed = true,
            paymentRequired = true,
            amount,
            noteVisibleToStudent = false,
            noteVisibleToParent = false
        });
        response.EnsureSuccessStatusCode();
    }
}
