using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Talmidon.Infrastructure.Identity;

namespace Talmidon.Tests;

/// <summary>
/// לתלמיד אין נקודות קצה שמקבלות מזהה של תלמיד אחר כפרמטר (בניגוד להורה, שיש לו כאלה —
/// ראו ParentIdorTests) — "my-schedule"/"my-notes" נגזרים תמיד מהתביעות (claims) של המשתמש
/// המחובר. לכן ה-IDOR הרלוונטי כאן הוא לוודא בפועל ששני תלמידים תחת אותה מורה לא "מדליפים"
/// זה לזה נתונים דרך אותן נקודות קצה, ושתלמיד לא יכול לפגוע ב-{id} של רשומה של תלמיד אחר
/// בנקודות קצה השמורות למורה (שם הוא נחסם לפי תפקיד, לא לפי בעלות — אבל התוצאה בפועל זהה).
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class StudentIdorTests(TalmidonWebApplicationFactory factory)
{
    [Fact]
    public async Task MySchedule_OnlyReturnsTheAuthenticatedStudentsOwnLessons()
    {
        var teacher = await TestHelpers.CreateAuthorizedTeacherClientAsync(factory, "idorStudentSched");
        var (studentAClient, studentAId) = await CreateStudentWithLoginAsync(teacher, "studentA");
        var (_, studentBId) = await CreateStudentWithLoginAsync(teacher, "studentB");

        var lessonAId = await CreateScheduledLessonAsync(teacher, studentAId);
        var lessonBId = await CreateScheduledLessonAsync(teacher, studentBId);

        var mySchedule = await studentAClient.GetFromJsonAsync<List<StudentLessonDto>>("/api/lessons/my-schedule");

        Assert.Contains(mySchedule!, l => l.Id == lessonAId);
        Assert.DoesNotContain(mySchedule!, l => l.Id == lessonBId);
    }

    [Fact]
    public async Task MyNotes_OnlyReturnsTheAuthenticatedStudentsOwnNotes()
    {
        var teacher = await TestHelpers.CreateAuthorizedTeacherClientAsync(factory, "idorStudentNotes");
        var (studentAClient, studentAId) = await CreateStudentWithLoginAsync(teacher, "studentC");
        var (_, studentBId) = await CreateStudentWithLoginAsync(teacher, "studentD");

        var noteAId = await CreateVisibleNoteAsync(teacher, studentAId);
        var noteBId = await CreateVisibleNoteAsync(teacher, studentBId);

        var myNotes = await studentAClient.GetFromJsonAsync<List<StudentNoteDto>>("/api/notes/my-notes");

        Assert.Contains(myNotes!, n => n.Id == noteAId);
        Assert.DoesNotContain(myNotes!, n => n.Id == noteBId);
    }

    [Fact]
    public async Task Student_CannotFetchAnotherStudentsNoteByIdViaTheTeacherEndpoint()
    {
        var teacher = await TestHelpers.CreateAuthorizedTeacherClientAsync(factory, "idorStudentDirect");
        var (studentAClient, _) = await CreateStudentWithLoginAsync(teacher, "studentE");
        var (_, studentBId) = await CreateStudentWithLoginAsync(teacher, "studentF");
        var noteBId = await CreateVisibleNoteAsync(teacher, studentBId);

        // /api/notes/{id} שמור למורה בלבד — תלמיד לא אמור להגיע אליו בכלל, גם לא לרשומה של עצמו.
        var response = await studentAClient.GetAsync($"/api/notes/{noteBId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ----- עזר -----

    private static async Task<Guid> CreateScheduledLessonAsync(HttpClient teacherClient, Guid studentId)
    {
        var response = await teacherClient.PostAsJsonAsync("/api/lessons", new
        {
            studentId,
            startTime = DateTimeOffset.UtcNow.AddDays(1),
            endTime = DateTimeOffset.UtcNow.AddDays(1).AddMinutes(45),
            reason = (string?)null
        });
        response.EnsureSuccessStatusCode();
        var lesson = await response.Content.ReadFromJsonAsync<LessonDto>();
        return lesson!.Id;
    }

    private static async Task<Guid> CreateVisibleNoteAsync(HttpClient teacherClient, Guid studentId)
    {
        var response = await teacherClient.PostAsJsonAsync("/api/notes", new
        {
            studentId,
            lessonId = (Guid?)null,
            content = "הערה לבדיקת IDOR",
            visibleToStudent = true,
            visibleToParent = true
        });
        response.EnsureSuccessStatusCode();
        var note = await response.Content.ReadFromJsonAsync<NoteDto>();
        return note!.Id;
    }

    private async Task<(HttpClient StudentClient, Guid StudentId)> CreateStudentWithLoginAsync(HttpClient teacherClient, string prefix)
    {
        var studentEmail = TestHelpers.UniqueEmail(prefix);
        var studentResponse = await teacherClient.PostAsJsonAsync("/api/students", new
        {
            fullName = $"תלמיד/ה {prefix}",
            gender = (int?)null,
            gradeLevel = (string?)null,
            birthDate = (string?)null,
            generalInfo = (string?)null,
            loginEmail = studentEmail,
            parentIds = Array.Empty<Guid>()
        });
        studentResponse.EnsureSuccessStatusCode();
        var student = await studentResponse.Content.ReadFromJsonAsync<StudentDto>();

        const string password = "StudentPass123";
        using (var scope = factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByEmailAsync(studentEmail) ?? throw new InvalidOperationException("Student user not found.");
            var token = await userManager.GeneratePasswordResetTokenAsync(user);
            var result = await userManager.ResetPasswordAsync(user, token, password);
            if (!result.Succeeded)
                throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));
            user.EmailConfirmed = true;
            await userManager.UpdateAsync(user);
        }

        var anon = factory.CreateClient();
        var accessToken = await TestHelpers.LoginAsync(anon, studentEmail, password);
        var studentClient = TestHelpers.AuthorizedClient(factory, accessToken);

        return (studentClient, student!.Id);
    }

    private record StudentDto(Guid Id, string FullName);
    private record LessonDto(Guid Id);
    private record NoteDto(Guid Id);
    private record StudentLessonDto(Guid Id, DateTimeOffset StartTime, DateTimeOffset EndTime, int Status, string? Homework);
    private record StudentNoteDto(Guid Id, string Content, DateTimeOffset CreatedAt);
}
