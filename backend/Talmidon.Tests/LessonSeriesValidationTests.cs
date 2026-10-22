using System.Net;
using System.Net.Http.Json;

namespace Talmidon.Tests;

/// <summary>
/// בדיקות התקינות של יצירת סדרה — כל צירוף שגוי של "עד מתי" נדחה עם הודעה שמסבירה מה
/// לא בסדר, לפני שנוצר שיעור כלשהו. לא היו מכוסות; המסלולים התקינים נבדקים ב-LessonSeriesTests.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class LessonSeriesValidationTests(TalmidonWebApplicationFactory factory)
{
    // ערכי ה-enum LessonSeriesEndCondition
    private const int Count = 0, EndDate = 1, Indefinite = 2;

    public static TheoryData<int, int?, string?, string> InvalidEndConditions => new()
    {
        { Count, null, null, "יש לציין מספר שיעורים." },
        { EndDate, null, null, "יש לציין תאריך סיום." },
        { Indefinite, 5, null, "סדרה ללא הגבלה לא יכולה לכלול מספר שיעורים או תאריך סיום." },
        { Indefinite, null, "2027-01-01", "סדרה ללא הגבלה לא יכולה לכלול מספר שיעורים או תאריך סיום." },
        { EndDate, 5, "2027-01-01", "מספר שיעורים רלוונטי רק במצב \"מספר שיעורים קבוע\"." },
        { Count, 5, "2027-01-01", "תאריך סיום רלוונטי רק במצב \"עד תאריך\"." }
    };

    [Theory]
    [MemberData(nameof(InvalidEndConditions))]
    public async Task InvalidEndCondition_IsRejected_WithAnExplanation_AndCreatesNothing(
        int endCondition, int? occurrenceCount, string? endDate, string expectedMessage)
    {
        var (teacher, studentId) = await TeacherWithStudentAsync();
        var start = TestHelpers.MiddayUtcIn(3);

        var response = await teacher.PostAsJsonAsync("/api/lesson-series", new
        {
            studentId,
            firstStartTime = start,
            firstEndTime = start.AddMinutes(45),
            endCondition,
            occurrenceCount,
            endDate
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(expectedMessage, (await response.Content.ReadFromJsonAsync<MessageDto>())!.Message);
        Assert.Empty((await teacher.GetFromJsonAsync<List<LessonDto>>($"/api/lessons?studentId={studentId}"))!);
    }

    [Fact]
    public async Task EndTimeBeforeStart_IsRejected()
    {
        var (teacher, studentId) = await TeacherWithStudentAsync();
        var start = TestHelpers.MiddayUtcIn(3);

        var response = await teacher.PostAsJsonAsync("/api/lesson-series", new
        {
            studentId,
            firstStartTime = start,
            firstEndTime = start,
            endCondition = Count,
            occurrenceCount = 3,
            endDate = (string?)null
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UnknownStudent_ReturnsNotFound()
    {
        var teacher = await TestHelpers.CreateAuthorizedTeacherClientAsync(factory, "seriesNoStudent");
        var start = TestHelpers.MiddayUtcIn(3);

        var response = await teacher.PostAsJsonAsync("/api/lesson-series", new
        {
            studentId = Guid.NewGuid(),
            firstStartTime = start,
            firstEndTime = start.AddMinutes(45),
            endCondition = Count,
            occurrenceCount = 3,
            endDate = (string?)null
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task<(HttpClient Teacher, Guid StudentId)> TeacherWithStudentAsync()
    {
        var teacher = await TestHelpers.CreateAuthorizedTeacherClientAsync(factory, "seriesValid");
        var response = await teacher.PostAsJsonAsync("/api/students", new
        {
            fullName = "תלמידת סדרה",
            gender = (int?)null,
            gradeLevel = (string?)null,
            birthDate = (string?)null,
            generalInfo = (string?)null,
            loginEmail = (string?)null,
            parentIds = Array.Empty<Guid>()
        });
        response.EnsureSuccessStatusCode();
        return (teacher, (await response.Content.ReadFromJsonAsync<IdDto>())!.Id);
    }

    private record IdDto(Guid Id);
    private record LessonDto(Guid Id);
    private record MessageDto(string Message);
}
