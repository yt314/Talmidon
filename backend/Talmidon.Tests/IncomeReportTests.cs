using System.Net;
using System.Net.Http.Json;
using System.Text;
using Talmidon.Api.Contracts;

namespace Talmidon.Tests;

/// <summary>
/// דוח ההכנסות וייצוא התשלומים — שניהם עוסקים בכסף ולא היו מכוסים. הנתונים נבנים דרך
/// ה-API (שיעור → סיום עם חיוב → תשלום), בחודש קבוע ורחוק כדי שהדוח לא יושפע מבדיקות אחרות.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class IncomeReportTests(TalmidonWebApplicationFactory factory)
{
    private static readonly DateTimeOffset May2029 = new(2029, 5, 7, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Income_SplitsChargedPaidAndOutstanding_ForTheMonthOnly()
    {
        var teacher = await TestHelpers.CreateAuthorizedTeacherClientAsync(factory, "income");
        var parentId = await AddParentAsync(teacher, "הורה משלם");
        var dana = await AddStudentAsync(teacher, "דנה", parentId);
        var yael = await AddStudentAsync(teacher, "יעל", parentId);

        var paidLesson = await CompletedLessonAsync(teacher, dana, May2029, charge: 100);
        await CompletedLessonAsync(teacher, dana, May2029.AddDays(1), charge: 150);
        await CompletedLessonAsync(teacher, yael, May2029.AddDays(2), charge: null);
        await CompletedLessonAsync(teacher, yael, May2029.AddMonths(1), charge: 500);
        await ScheduledLessonAsync(teacher, yael, May2029.AddDays(3));
        await PayAsync(teacher, parentId, paidLesson);

        var report = await teacher.GetFromJsonAsync<IncomeReportDto>("/api/reports/income?year=2029&month=5");

        Assert.Equal((3, 250m, 100m, 150m),
            (report!.CompletedLessons, report.TotalCharged, report.TotalPaid, report.TotalOutstanding));
        // ממוין לפי החיוב, מהגבוה
        Assert.Equal(new[] { ("דנה", 2, 250m, 100m), ("יעל", 1, 0m, 0m) },
            report.ByStudent.Select(s => (s.StudentName, s.Lessons, s.Charged, s.Paid)));
    }

    [Fact]
    public async Task Income_ForAnInvalidMonth_IsRejected()
    {
        var teacher = await TestHelpers.CreateAuthorizedTeacherClientAsync(factory, "incomeBadMonth");

        var response = await teacher.GetAsync("/api/reports/income?year=2029&month=0");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PaymentsExport_ListsThePayment_WithWhoPaidAndForWhom()
    {
        var teacher = await TestHelpers.CreateAuthorizedTeacherClientAsync(factory, "exportPayments");
        var parentId = await AddParentAsync(teacher, "רחל המשלמת");
        var student = await AddStudentAsync(teacher, "נועה", parentId);
        var lesson = await CompletedLessonAsync(teacher, student, May2029, charge: 180);
        await PayAsync(teacher, parentId, lesson, method: "העברה");

        var response = await teacher.GetAsync("/api/export/payments.csv");
        response.EnsureSuccessStatusCode();
        var csv = Encoding.UTF8.GetString(await response.Content.ReadAsByteArrayAsync());

        var row = Assert.Single(csv.Split('\n'), l => l.Contains("רחל המשלמת"));
        Assert.Contains("180", row);
        Assert.Contains("העברה", row);
        Assert.Contains("נועה", row);
    }

    // ----- עזר -----

    private static async Task<Guid> AddParentAsync(HttpClient teacher, string fullName)
    {
        var response = await teacher.PostAsJsonAsync("/api/parents", new
        {
            fullName, gender = (int?)null, email = TestHelpers.UniqueEmail("incomeParent"), phone = (string?)null
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdDto>())!.Id;
    }

    private static async Task<Guid> AddStudentAsync(HttpClient teacher, string fullName, Guid parentId)
    {
        var response = await teacher.PostAsJsonAsync("/api/students", new
        {
            fullName, gender = (int?)null, gradeLevel = (string?)null, birthDate = (string?)null,
            generalInfo = (string?)null, loginEmail = (string?)null, parentIds = new[] { parentId }
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdDto>())!.Id;
    }

    private static async Task<Guid> ScheduledLessonAsync(HttpClient teacher, Guid studentId, DateTimeOffset start)
    {
        var response = await teacher.PostAsJsonAsync("/api/lessons", new { studentId, startTime = start, endTime = start.AddMinutes(45) });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdDto>())!.Id;
    }

    /// <param name="charge">null = התקיים בלי חיוב.</param>
    private static async Task<Guid> CompletedLessonAsync(HttpClient teacher, Guid studentId, DateTimeOffset start, decimal? charge)
    {
        var id = await ScheduledLessonAsync(teacher, studentId, start);
        var response = await teacher.PostAsJsonAsync($"/api/lessons/{id}/complete", new
        {
            completed = true,
            paymentRequired = charge is not null,
            amount = charge ?? 0,
            homework = (string?)null,
            noteContent = (string?)null,
            noteVisibleToStudent = false,
            noteVisibleToParent = false
        });
        response.EnsureSuccessStatusCode();
        return id;
    }

    private static async Task PayAsync(HttpClient teacher, Guid parentId, Guid lessonId, string? method = null)
    {
        var response = await teacher.PostAsJsonAsync("/api/payments", new
        {
            parentId, lessonIds = new[] { lessonId }, paidDate = new DateOnly(2029, 5, 20), method, note = (string?)null
        });
        response.EnsureSuccessStatusCode();
    }

    private record IdDto(Guid Id);
}
