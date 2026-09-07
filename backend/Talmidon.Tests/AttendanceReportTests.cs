using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Talmidon.Api.Contracts;
using Talmidon.Domain.Entities;
using Talmidon.Domain.Enums;
using Talmidon.Infrastructure.Data;

namespace Talmidon.Tests;

/// <summary>
/// דוח הנוכחות. השיעורים נכתבים ישירות למסד כדי לקבוע סטטוסים ומשכים מדויקים — המסלול
/// דרך ה-API עובר אישורים והתראות שאינם נושא הבדיקה הזו.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class AttendanceReportTests(TalmidonWebApplicationFactory factory)
{
    [Fact]
    public async Task CountsStatuses_SumsHours_AndRanksByMissedPercent()
    {
        var teacher = await TestHelpers.CreateAuthorizedTeacherClientAsync(factory, "attendance");
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TalmidonDbContext>();

        // התלמידות נוצרות דרך ה-API, כדי שהדייר יהיה של המורה הזו ולא של מורה אחרת שנוצרה בבדיקה מקבילה
        var reliable = await AddStudentAsync(teacher, "תלמידה מתמידה");
        var flaky = await AddStudentAsync(teacher, "תלמידה מבטלת");
        var tenantId = (await db.Students.IgnoreQueryFilters().FirstAsync(s => s.Id == reliable)).TenantId;

        // חודש קבוע ורחוק, כדי שהבדיקה לא תושפע מנתונים אחרים או מהתאריך שבו היא רצה
        var month = new DateTimeOffset(2029, 4, 1, 0, 0, 0, TimeSpan.Zero);

        // מתמידה: שלושה שיעורים של שעה שהתקיימו, אף החמצה
        for (var i = 0; i < 3; i++)
            AddLesson(db, tenantId, reliable, month.AddDays(i).AddHours(9), 60, LessonStatus.Completed);

        // מבטלת: שיעור אחד של 90 דקות שהתקיים, אחד בוטל ואחד לא הגיע → שני שליש החמצה
        AddLesson(db, tenantId, flaky, month.AddDays(1).AddHours(14), 90, LessonStatus.Completed);
        AddLesson(db, tenantId, flaky, month.AddDays(2).AddHours(14), 60, LessonStatus.Cancelled);
        AddLesson(db, tenantId, flaky, month.AddDays(3).AddHours(14), 60, LessonStatus.NoShow);

        // מתוזמן בלבד — אינו נספר כלל, לא כהתקיים ולא כהחמצה
        AddLesson(db, tenantId, flaky, month.AddDays(4).AddHours(14), 60, LessonStatus.Scheduled);

        await db.SaveChangesAsync();

        var report = await teacher.GetFromJsonAsync<AttendanceReportDto>("/api/reports/attendance?year=2029&month=4");

        Assert.Equal(4, report!.Completed);
        Assert.Equal(1, report.Cancelled);
        Assert.Equal(1, report.NoShow);
        // שלוש שעות + שעה וחצי
        Assert.Equal(4.5m, report.Hours);

        // הדוח מסודר לפי שיעור ההחמצה — מי שמבטל הרבה נמצא למעלה
        Assert.Equal("תלמידה מבטלת", report.ByStudent[0].StudentName);
        Assert.Equal(67, report.ByStudent[0].MissedPercent);
        Assert.Equal(1.5m, report.ByStudent[0].Hours);

        var second = report.ByStudent[1];
        Assert.Equal("תלמידה מתמידה", second.StudentName);
        Assert.Equal(0, second.MissedPercent);
        Assert.Equal(3, second.Completed);
        Assert.Equal(3m, second.Hours);
    }

    [Fact]
    public async Task InvalidMonthIsRejected()
    {
        var teacher = await TestHelpers.CreateAuthorizedTeacherClientAsync(factory, "attendanceBadMonth");

        var response = await teacher.GetAsync("/api/reports/attendance?year=2029&month=13");

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static async Task<Guid> AddStudentAsync(HttpClient teacher, string name)
    {
        var response = await teacher.PostAsJsonAsync("/api/students", new
        {
            fullName = name,
            gender = (int?)null,
            gradeLevel = (string?)null,
            birthDate = (string?)null,
            generalInfo = (string?)null,
            loginEmail = (string?)null,
            parentIds = Array.Empty<Guid>()
        });
        response.EnsureSuccessStatusCode();
        var student = await response.Content.ReadFromJsonAsync<StudentDetailDto>();
        return student!.Id;
    }

    private static void AddLesson(
        TalmidonDbContext db, Guid tenantId, Guid studentId, DateTimeOffset start, int minutes, LessonStatus status)
    {
        db.Lessons.Add(new Lesson
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            StudentId = studentId,
            StartTime = start,
            EndTime = start.AddMinutes(minutes),
            Status = status,
            Origin = LessonOrigin.Teacher
        });
    }
}
