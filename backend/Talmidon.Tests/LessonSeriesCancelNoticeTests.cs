using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Talmidon.Api.Contracts;
using Talmidon.Domain.Common;
using Talmidon.Infrastructure.Identity;

namespace Talmidon.Tests;

/// <summary>
/// ביטול שיעור קבוע שגם מוחק מופעים עתידיים. עד לשינוי הזה הפעולה מחקה שורה
/// אחר שורה מהיומן של המשפחה בלי להודיע לאיש — וזה המקרה הגדול מכולם, כי מדובר
/// בכל השיעורים שנשארו ולא באחד.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class LessonSeriesCancelNoticeTests(TalmidonWebApplicationFactory factory)
{
    private const string StudentName = "רוני בן-דוד";

    [Fact]
    public async Task CancellingWithDeletion_TellsTheFamilyHowManyAndWhen()
    {
        var f = await CreateFamilyAsync("seriesNotice", withStudentLogin: true);
        var seriesId = await CreateSeriesAsync(f.Teacher, f.StudentId, occurrences: 4);

        var response = await f.Teacher.DeleteAsync($"/api/lesson-series/{seriesId}?deleteFutureOccurrences=true");
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<CancelLessonSeriesResultDto>();

        Assert.Equal(4, result!.CancelledCount);

        var toParent = Assert.Single(factory.SentEmails.To(f.ParentEmail), e => e.Subject.Contains("השיעור הקבוע"));
        Assert.Contains("4 שיעורים בוטלו", toParent.Subject);
        Assert.Contains(StudentName, toParent.Subject);
        // היומן של התלמידה התרוקן, ולכן גם היא מקבלת
        Assert.Contains(factory.SentEmails.To(f.StudentEmail), e => e.Subject.Contains("השיעור הקבוע"));
    }

    /// <summary>המייל נושא את התאריכים עצמם — "4 שיעורים בוטלו" בלי לומר אילו אינו תשובה.</summary>
    [Fact]
    public async Task TheNoticeListsTheDatesThatWereCancelled()
    {
        var f = await CreateFamilyAsync("seriesDates");
        var seriesId = await CreateSeriesAsync(f.Teacher, f.StudentId, occurrences: 3);

        var lessons = await f.Teacher.GetFromJsonAsync<List<LessonDto>>($"/api/lessons?studentId={f.StudentId}");
        var expected = lessons!.OrderBy(l => l.StartTime).Select(l => l.StartTime).ToList();

        (await f.Teacher.DeleteAsync($"/api/lesson-series/{seriesId}?deleteFutureOccurrences=true")).EnsureSuccessStatusCode();

        var mail = Assert.Single(factory.SentEmails.To(f.ParentEmail), e => e.Subject.Contains("השיעור הקבוע"));
        // בשעון ישראל, לא ב-UTC: זה מה שקורא המייל מחזיק בראש
        foreach (var date in expected.Select(AppTimeZone.ToLocal))
            Assert.Contains($"{date:dd/MM/yyyy} בשעה {date:HH:mm}", mail.HtmlBody);
    }

    /// <summary>
    /// בלי מחיקת מופעים לא השתנה דבר ביומן של המשפחה: אין על מה להודיע, ומייל
    /// כזה היה מבהיל על לא כלום.
    /// </summary>
    [Fact]
    public async Task CancellingWithoutDeletion_SendsNothing()
    {
        var f = await CreateFamilyAsync("seriesQuiet");
        var seriesId = await CreateSeriesAsync(f.Teacher, f.StudentId, occurrences: 3);

        var response = await f.Teacher.DeleteAsync($"/api/lesson-series/{seriesId}");
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<CancelLessonSeriesResultDto>();

        Assert.Equal(0, result!.CancelledCount);
        Assert.DoesNotContain(factory.SentEmails.To(f.ParentEmail), e => e.Subject.Contains("השיעור הקבוע"));
    }

    // ----- עזר -----

    private sealed record Family(HttpClient Teacher, Guid StudentId, string ParentEmail, string StudentEmail);

    private static async Task<Guid> CreateSeriesAsync(HttpClient teacher, Guid studentId, int occurrences)
    {
        // יום קבוע בעתיד, כדי שכל המופעים ייווצרו אחרי "עכשיו"
        var firstStart = DateTimeOffset.UtcNow.AddDays(3);
        firstStart = new DateTimeOffset(firstStart.Year, firstStart.Month, firstStart.Day, 16, 0, 0, TimeSpan.Zero);

        var response = await teacher.PostAsJsonAsync("/api/lesson-series", new
        {
            studentId,
            firstStartTime = firstStart,
            firstEndTime = firstStart.AddHours(1),
            endCondition = 0, // Count
            occurrenceCount = occurrences,
            endDate = (DateOnly?)null,
            skipJewishHolidays = false
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SeriesDto>())!.Id;
    }

    private async Task<Family> CreateFamilyAsync(string prefix, bool withStudentLogin = false)
    {
        var teacher = await TestHelpers.CreateAuthorizedTeacherClientAsync(factory, $"{prefix}T");

        var parentEmail = TestHelpers.UniqueEmail($"{prefix}P");
        var parentResponse = await teacher.PostAsJsonAsync("/api/parents", new
        {
            fullName = "הורה בדיקה",
            gender = (int?)null,
            email = parentEmail,
            phone = (string?)null
        });
        parentResponse.EnsureSuccessStatusCode();
        var parent = await parentResponse.Content.ReadFromJsonAsync<ParentIdDto>();

        var studentEmail = withStudentLogin ? TestHelpers.UniqueEmail($"{prefix}S") : null;
        var studentResponse = await teacher.PostAsJsonAsync("/api/students", new
        {
            fullName = StudentName,
            gender = (int?)null,
            gradeLevel = (string?)null,
            birthDate = (string?)null,
            generalInfo = (string?)null,
            loginEmail = studentEmail,
            parentIds = new[] { parent!.Id }
        });
        studentResponse.EnsureSuccessStatusCode();
        var student = await studentResponse.Content.ReadFromJsonAsync<StudentDetailDto>();

        return new Family(teacher, student!.Id, parentEmail, studentEmail ?? "");
    }

    private record ParentIdDto(Guid Id);
    private record SeriesDto(Guid Id);
    private record LessonDto(Guid Id, DateTimeOffset StartTime);
}
