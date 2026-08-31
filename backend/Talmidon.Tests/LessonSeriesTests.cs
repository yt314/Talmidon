using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Talmidon.Domain.Common;
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

    /// <summary>
    /// לפני התיקון, כל מופע נבנה מ"הרכבת" תאריך+שעת-יום ישירות כ-UTC — כך ששעת-היום המקומית
    /// (ישראל) הייתה זזה שעה בכל מעבר שעון קיץ/חורף. עכשיו כל מופע נבנה מהזמן המקומי ורק אז
    /// מומר ל-UTC, כך שהשעה המקומית נשארת קבועה וה-offset הוא זה שמשתנה.
    /// </summary>
    [Fact]
    public async Task GenerateOccurrencesAsync_AcrossDstTransition_KeepsLocalWallClockHourFixed()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TalmidonDbContext>();
        var generator = scope.ServiceProvider.GetRequiredService<LessonSeriesGenerator>();

        var (_, studentId) = await CreateTeacherWithStudentAsync("seriesDst");
        var tenantId = (await db.Students.IgnoreQueryFilters().FirstAsync(s => s.Id == studentId)).TenantId;

        var transitionDate = FindNextDstTransition(new DateOnly(2026, 1, 1));
        var seriesStartDate = transitionDate.AddDays(-14);

        var series = new LessonSeries
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            StudentId = studentId,
            DayOfWeek = seriesStartDate.DayOfWeek,
            StartTimeOfDay = new TimeOnly(16, 0),
            DurationMinutes = 60,
            SeriesStartDate = seriesStartDate,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.LessonSeries.Add(series);
        await db.SaveChangesAsync();

        await generator.GenerateOccurrencesAsync(series, seriesStartDate.AddDays(28));

        var occurrences = await db.Lessons.IgnoreQueryFilters()
            .Where(l => l.SeriesId == series.Id)
            .OrderBy(l => l.StartTime)
            .ToListAsync();

        Assert.True(occurrences.Count >= 4, "יש לוודא שנוצרו מספיק מופעים משני צדי מעבר השעון.");
        Assert.All(occurrences, l => Assert.Equal(new TimeOnly(16, 0), TimeOnly.FromDateTime(AppTimeZone.ToLocal(l.StartTime).DateTime)));

        // מה שנשמר ב-DB תמיד מנורמל ל-offset=0 (Npgsql דורש UTC טהור), אז ה-offset עצמו כבר לא
        // משתנה אחרי round-trip. אבל שעת ה-UTC כן משתנה בין חורף לקיץ — וזה בדיוק מה שמוכיח
        // שהזמן המקומי, לא ה-UTC הגולמי, הוא הקבוע (אחרת שעת ה-UTC הייתה זהה בשני הצדדים).
        Assert.NotEqual(occurrences.First().StartTime.UtcDateTime.Hour, occurrences.Last().StartTime.UtcDateTime.Hour);
    }

    /// <summary>מוצא את התאריך הקרוב ביותר (אחרי <paramref name="from"/>) שבו שעון הקיץ באזור הזמן של האפליקציה מתחלף.</summary>
    private static DateOnly FindNextDstTransition(DateOnly from)
    {
        var date = from.ToDateTime(TimeOnly.MinValue);
        var wasDst = AppTimeZone.Instance.IsDaylightSavingTime(date);
        for (var i = 0; i < 400; i++)
        {
            date = date.AddDays(1);
            if (AppTimeZone.Instance.IsDaylightSavingTime(date) != wasDst)
                return DateOnly.FromDateTime(date);
        }
        throw new InvalidOperationException("No DST transition found within a year — is AppTimeZone.Instance correct?");
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
