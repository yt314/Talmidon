using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Talmidon.Api.Contracts;
using Talmidon.Domain.Enums;
using Talmidon.Infrastructure.Data;
using Talmidon.Infrastructure.Identity;

namespace Talmidon.Tests;

/// <summary>
/// בקשת שיעור מתלמיד. הבקשה נכנסת כ-Requested וממתינה לאישור המורה — תלמיד אינו יכול
/// לקבוע לעצמו שיעור, רק לבקש.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class StudentLessonRequestTests(TalmidonWebApplicationFactory factory)
{
    [Fact]
    public async Task Student_CanRequestALesson_AndItWaitsForApproval()
    {
        var (teacher, student, studentId) = await CreateStudentWithLoginAsync("studentReq");
        var start = DateTimeOffset.UtcNow.AddDays(3);

        var response = await student.PostAsJsonAsync("/api/lessons/my-requests", new
        {
            startTime = start,
            endTime = start.AddHours(1),
            reason = "יש לי מבחן בשבוע הבא"
        });
        response.EnsureSuccessStatusCode();

        var created = await response.Content.ReadFromJsonAsync<LessonDto>();
        Assert.Equal(LessonStatus.Requested, created!.Status);
        Assert.Equal(studentId, created.StudentId);

        // המורה רואה את הבקשה ביומן שלה
        var teacherLessons = await teacher.GetFromJsonAsync<List<LessonDto>>($"/api/lessons?studentId={studentId}");
        Assert.Contains(teacherLessons!, l => l.Id == created.Id && l.Status == LessonStatus.Requested);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TalmidonDbContext>();
        var stored = await db.Lessons.IgnoreQueryFilters().FirstAsync(l => l.Id == created.Id);
        Assert.Equal(LessonOrigin.Student, stored.Origin);
        // בקשת תלמיד אינה מגיעה מהורה, ולכן השדה נשאר ריק
        Assert.Null(stored.RequestedByParentId);
    }

    /// <summary>
    /// הסיבה שהתלמיד כותב חייבת להגיע למורה. עד לשינוי הזה היא התקבלה בשרת ונזרקה,
    /// כך שמי שטרח להסביר — הסביר לאף אחד.
    /// </summary>
    [Fact]
    public async Task TheReasonReachesTheTeacherNotification()
    {
        var (teacher, student, _) = await CreateStudentWithLoginAsync("studentReason");
        var start = DateTimeOffset.UtcNow.AddDays(4);

        var response = await student.PostAsJsonAsync("/api/lessons/my-requests", new
        {
            startTime = start,
            endTime = start.AddHours(1),
            reason = "מתקשה בשברים"
        });
        response.EnsureSuccessStatusCode();

        var notifications = await teacher.GetFromJsonAsync<List<NotificationDto>>("/api/notifications");
        Assert.Contains(notifications!, n => n.Message.Contains("מתקשה בשברים"));
    }

    [Fact]
    public async Task EndBeforeStartIsRejected()
    {
        var (_, student, _) = await CreateStudentWithLoginAsync("studentReqOrder");
        var start = DateTimeOffset.UtcNow.AddDays(2);

        var response = await student.PostAsJsonAsync("/api/lessons/my-requests", new
        {
            startTime = start,
            endTime = start.AddHours(-1),
            reason = (string?)null
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>המורה עצמה קובעת שיעורים במסלול אחר — הנתיב הזה אינו פתוח לה.</summary>
    [Fact]
    public async Task TeacherCannotUseTheStudentRoute()
    {
        var (teacher, _, _) = await CreateStudentWithLoginAsync("studentReqRole");
        var start = DateTimeOffset.UtcNow.AddDays(2);

        var response = await teacher.PostAsJsonAsync("/api/lessons/my-requests", new
        {
            startTime = start,
            endTime = start.AddHours(1),
            reason = (string?)null
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>יוצרת תלמידה עם חשבון התחברות ומחזירה קליינט מחובר — כמו ב-StudentIdorTests.</summary>
    private async Task<(HttpClient Teacher, HttpClient Student, Guid StudentId)> CreateStudentWithLoginAsync(string prefix)
    {
        var teacher = await TestHelpers.CreateAuthorizedTeacherClientAsync(factory, $"{prefix}T");
        var loginEmail = TestHelpers.UniqueEmail($"{prefix}S");

        var created = await teacher.PostAsJsonAsync("/api/students", new
        {
            fullName = "תלמידה מבקשת",
            gender = (int?)null,
            gradeLevel = (string?)null,
            birthDate = (string?)null,
            generalInfo = (string?)null,
            loginEmail,
            parentIds = Array.Empty<Guid>()
        });
        created.EnsureSuccessStatusCode();
        var student = await created.Content.ReadFromJsonAsync<StudentDetailDto>();

        const string password = "StudentPass123";
        using (var scope = factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByEmailAsync(loginEmail)
                ?? throw new InvalidOperationException("Student user not found.");
            var token = await userManager.GeneratePasswordResetTokenAsync(user);
            var result = await userManager.ResetPasswordAsync(user, token, password);
            if (!result.Succeeded)
                throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));
            user.EmailConfirmed = true;
            await userManager.UpdateAsync(user);
        }

        var anon = factory.CreateClient();
        var accessToken = await TestHelpers.LoginAsync(anon, loginEmail, password);

        return (teacher, TestHelpers.AuthorizedClient(factory, accessToken), student!.Id);
    }
}
