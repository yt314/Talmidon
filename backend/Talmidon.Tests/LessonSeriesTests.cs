using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Talmidon.Domain.Entities;
using Talmidon.Infrastructure.Data;
using Talmidon.Infrastructure.Scheduling;

namespace Talmidon.Tests;

/// <summary>
/// סדרות שיעורים חוזרות: יצירה מייצרת מיד את המופעים לפי תנאי הסיום שנבחר, וכל מופע הוא
/// שורת Lesson רגילה ועצמאית (ניתן להשלים/למחוק בודד בלי לפגוע בשאר הסדרה). ביטול סדרה
/// לעולם לא נוגע בשיעורים שכבר טופלו (הושלמו), ומכבד את דגל deleteFutureOccurrences לגבי
/// מופעים עתידיים שעדיין מתוזמנים.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class LessonSeriesTests(TalmidonWebApplicationFactory factory)
{
    [Fact]
    public async Task Create_WithFixedCount_CreatesExactlyThatManyOccurrences()
    {
        var (teacher, studentId) = await CreateTeacherWithStudentAsync("seriesCount");
        var firstStart = NextWeekday(DayOfWeek.Tuesday, hour: 16);

        var response = await teacher.PostAsJsonAsync("/api/lesson-series", new
        {
            studentId,
            firstStartTime = firstStart,
            firstEndTime = firstStart.AddHours(1),
            endCondition = 0, // Count
            occurrenceCount = 4,
            endDate = (DateOnly?)null
        });
        response.EnsureSuccessStatusCode();
        var series = await response.Content.ReadFromJsonAsync<LessonSeriesDto>();

        Assert.Equal(4, series!.OccurrencesCreated);

        var lessons = await teacher.GetFromJsonAsync<List<LessonDto>>($"/api/lessons?studentId={studentId}");
        Assert.Equal(4, lessons!.Count);
        Assert.All(lessons, l => Assert.NotNull(l.SeriesId));
    }

    [Fact]
    public async Task Create_WithEndDate_StopsAtOrBeforeEndDate()
    {
        var (teacher, studentId) = await CreateTeacherWithStudentAsync("seriesEndDate");
        var firstStart = NextWeekday(DayOfWeek.Wednesday, hour: 10);
        // שלושה מופעים בדיוק: יום 0, 7, 14 — תאריך הסיום נופל בול על המופע השלישי.
        var endDate = DateOnly.FromDateTime(firstStart.AddDays(14).UtcDateTime);

        var response = await teacher.PostAsJsonAsync("/api/lesson-series", new
        {
            studentId,
            firstStartTime = firstStart,
            firstEndTime = firstStart.AddMinutes(45),
            endCondition = 1, // EndDate
            occurrenceCount = (int?)null,
            endDate
        });
        response.EnsureSuccessStatusCode();
        var series = await response.Content.ReadFromJsonAsync<LessonSeriesDto>();

        Assert.Equal(3, series!.OccurrencesCreated);
    }

    [Fact]
    public async Task Cancel_WithDeleteFutureOccurrences_KeepsCompletedButRemovesFutureScheduled()
    {
        var (teacher, studentId) = await CreateTeacherWithStudentAsync("seriesCancelDelete");
        var firstStart = NextWeekday(DayOfWeek.Thursday, hour: 14);

        var createResponse = await teacher.PostAsJsonAsync("/api/lesson-series", new
        {
            studentId,
            firstStartTime = firstStart,
            firstEndTime = firstStart.AddHours(1),
            endCondition = 0,
            occurrenceCount = 3,
            endDate = (DateOnly?)null
        });
        createResponse.EnsureSuccessStatusCode();
        var series = await createResponse.Content.ReadFromJsonAsync<LessonSeriesDto>();

        var lessonsBeforeCancel = await teacher.GetFromJsonAsync<List<LessonDto>>($"/api/lessons?studentId={studentId}");
        var firstOccurrence = lessonsBeforeCancel!.OrderBy(l => l.StartTime).First();

        // "משלימים" את המופע הראשון — לא אמור להימחק גם עם deleteFutureOccurrences=true.
        var completeResponse = await teacher.PostAsJsonAsync($"/api/lessons/{firstOccurrence.Id}/complete", new
        {
            completed = true,
            paymentRequired = false,
            amount = 0,
            homework = (string?)null,
            noteContent = (string?)null,
            noteVisibleToStudent = false,
            noteVisibleToParent = false
        });
        completeResponse.EnsureSuccessStatusCode();

        var cancelResponse = await teacher.DeleteAsync($"/api/lesson-series/{series!.Id}?deleteFutureOccurrences=true");
        Assert.Equal(HttpStatusCode.NoContent, cancelResponse.StatusCode);

        var lessonsAfterCancel = await teacher.GetFromJsonAsync<List<LessonDto>>($"/api/lessons?studentId={studentId}");
        Assert.Single(lessonsAfterCancel!);
        Assert.Equal(firstOccurrence.Id, lessonsAfterCancel![0].Id);
    }

    [Fact]
    public async Task Cancel_WithoutDeleteFutureOccurrences_LeavesThemStanding()
    {
        var (teacher, studentId) = await CreateTeacherWithStudentAsync("seriesCancelKeep");
        var firstStart = NextWeekday(DayOfWeek.Friday, hour: 9);

        var createResponse = await teacher.PostAsJsonAsync("/api/lesson-series", new
        {
            studentId,
            firstStartTime = firstStart,
            firstEndTime = firstStart.AddMinutes(45),
            endCondition = 0,
            occurrenceCount = 3,
            endDate = (DateOnly?)null
        });
        createResponse.EnsureSuccessStatusCode();
        var series = await createResponse.Content.ReadFromJsonAsync<LessonSeriesDto>();

        var cancelResponse = await teacher.DeleteAsync($"/api/lesson-series/{series!.Id}?deleteFutureOccurrences=false");
        Assert.Equal(HttpStatusCode.NoContent, cancelResponse.StatusCode);

        var lessonsAfterCancel = await teacher.GetFromJsonAsync<List<LessonDto>>($"/api/lessons?studentId={studentId}");
        Assert.Equal(3, lessonsAfterCancel!.Count);
    }

    [Fact]
    public async Task Generator_CalledTwiceWithSameHorizon_IsIdempotent()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TalmidonDbContext>();
        var generator = scope.ServiceProvider.GetRequiredService<LessonSeriesGenerator>();

        var (teacher, studentId) = await CreateTeacherWithStudentAsync("seriesIdempotent");
        var firstStart = NextWeekday(DayOfWeek.Monday, hour: 18);

        var createResponse = await teacher.PostAsJsonAsync("/api/lesson-series", new
        {
            studentId,
            firstStartTime = firstStart,
            firstEndTime = firstStart.AddHours(1),
            endCondition = 2, // Indefinite
            occurrenceCount = (int?)null,
            endDate = (DateOnly?)null
        });
        createResponse.EnsureSuccessStatusCode();
        var seriesDto = await createResponse.Content.ReadFromJsonAsync<LessonSeriesDto>();

        var horizon = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(56);
        var series = await db.LessonSeries.IgnoreQueryFilters().FirstAsync(s => s.Id == seriesDto!.Id);

        var secondCall = await generator.GenerateOccurrencesAsync(series, horizon);

        Assert.Equal(0, secondCall);
    }

    // ----- עזר -----

    /// <summary>הפעם הבאה שיום-בשבוע הנתון חל, בשעה נתונה, לפחות שבוע קדימה — כדי לא להתנגש בזמן הריצה הנוכחי.</summary>
    private static DateTimeOffset NextWeekday(DayOfWeek dayOfWeek, int hour)
    {
        var date = DateTime.UtcNow.Date.AddDays(8);
        while (date.DayOfWeek != dayOfWeek)
            date = date.AddDays(1);
        return new DateTimeOffset(date.AddHours(hour), TimeSpan.Zero);
    }

    private async Task<(HttpClient Teacher, Guid StudentId)> CreateTeacherWithStudentAsync(string prefix)
    {
        var teacher = await TestHelpers.CreateAuthorizedTeacherClientAsync(factory, $"{prefix}T");

        var studentResponse = await teacher.PostAsJsonAsync("/api/students", new
        {
            fullName = "תלמיד לבדיקת סדרה",
            gender = (int?)null,
            gradeLevel = (string?)null,
            birthDate = (string?)null,
            generalInfo = (string?)null,
            loginEmail = (string?)null,
            parentIds = Array.Empty<Guid>()
        });
        studentResponse.EnsureSuccessStatusCode();
        var student = await studentResponse.Content.ReadFromJsonAsync<StudentDto>();

        return (teacher, student!.Id);
    }

    private record StudentDto(Guid Id, string FullName);
    private record LessonDto(Guid Id, DateTimeOffset StartTime, DateTimeOffset EndTime, Guid? SeriesId);
    private record LessonSeriesDto(Guid Id, Guid StudentId, string StudentName, DayOfWeek DayOfWeek, TimeOnly StartTimeOfDay,
        int DurationMinutes, DateOnly? EndDate, int? OccurrenceCount, int OccurrencesGenerated, bool IsActive, int OccurrencesCreated);
}
