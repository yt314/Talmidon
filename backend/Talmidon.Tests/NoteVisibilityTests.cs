using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Talmidon.Infrastructure.Identity;

namespace Talmidon.Tests;

/// <summary>
/// כלל שנאכף בשרת (NotesController.Create/Update): הערה שגלויה לתלמיד היא תמיד גלויה גם
/// להורה, גם אם המורה לא סימנה VisibleToParent במפורש — כדי שהורה לעולם לא יופתע מהערה
/// שהילד/ה שלו רואה אבל הוא לא. בודק גם את שני צדדי ה-opt-out (לא גלוי להורה / לא גלוי לתלמיד)
/// כדי לוודא שההסתרה בכל צד עובדת בפועל, לא רק שהדגל החיובי נאכף.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class NoteVisibilityTests(TalmidonWebApplicationFactory factory)
{
    [Fact]
    public async Task Create_VisibleToStudentOnly_ServerAlsoMarksVisibleToParent()
    {
        var (teacher, _, _, studentId) = await CreateTeacherParentStudentAsync("noteCreateUpgrade");

        var response = await teacher.PostAsJsonAsync("/api/notes", new
        {
            studentId,
            lessonId = (Guid?)null,
            content = "שיעורי בית: תרגילים 1-10",
            visibleToStudent = true,
            visibleToParent = false
        });
        response.EnsureSuccessStatusCode();

        var note = await response.Content.ReadFromJsonAsync<NoteDto>();
        Assert.True(note!.VisibleToParent);
    }

    [Fact]
    public async Task Update_VisibleToStudentOnly_ServerAlsoMarksVisibleToParent()
    {
        var (teacher, _, _, studentId) = await CreateTeacherParentStudentAsync("noteUpdateUpgrade");
        var noteId = await CreateNoteAsync(teacher, studentId, visibleToStudent: false, visibleToParent: false);

        var update = await teacher.PutAsJsonAsync($"/api/notes/{noteId}", new
        {
            content = "עודכן: יש להתאמן גם על שברים",
            visibleToStudent = true,
            visibleToParent = false
        });
        update.EnsureSuccessStatusCode();

        var note = await teacher.GetFromJsonAsync<NoteDto>($"/api/notes/{noteId}");
        Assert.True(note!.VisibleToStudent);
        Assert.True(note.VisibleToParent);
    }

    [Fact]
    public async Task ParentsMineEndpoint_OnlyReturnsNotesMarkedVisibleToParent()
    {
        var (teacher, parent, _, studentId) = await CreateTeacherParentStudentAsync("noteParentHidden");
        await CreateNoteAsync(teacher, studentId, visibleToStudent: false, visibleToParent: false);
        var visibleNoteId = await CreateNoteAsync(teacher, studentId, visibleToStudent: false, visibleToParent: true);

        var notes = await parent.GetFromJsonAsync<List<ParentNoteDto>>("/api/notes/mine");

        Assert.Single(notes!);
        Assert.Equal(visibleNoteId, notes![0].Id);
    }

    [Fact]
    public async Task StudentsMyNotesEndpoint_OnlyReturnsNotesMarkedVisibleToStudent()
    {
        var (teacher, _, student, studentId) = await CreateTeacherParentStudentAsync("noteStudentHidden");
        await CreateNoteAsync(teacher, studentId, visibleToStudent: false, visibleToParent: true);
        var visibleNoteId = await CreateNoteAsync(teacher, studentId, visibleToStudent: true, visibleToParent: true);

        var notes = await student.GetFromJsonAsync<List<StudentNoteDto>>("/api/notes/my-notes");

        Assert.Single(notes!);
        Assert.Equal(visibleNoteId, notes![0].Id);
    }

    // ----- עזר -----

    private static async Task<Guid> CreateNoteAsync(HttpClient teacherClient, Guid studentId, bool visibleToStudent, bool visibleToParent)
    {
        var response = await teacherClient.PostAsJsonAsync("/api/notes", new
        {
            studentId,
            lessonId = (Guid?)null,
            content = "הערה לבדיקה",
            visibleToStudent,
            visibleToParent
        });
        response.EnsureSuccessStatusCode();
        var note = await response.Content.ReadFromJsonAsync<NoteDto>();
        return note!.Id;
    }

    private async Task<(HttpClient Teacher, HttpClient Parent, HttpClient Student, Guid StudentId)> CreateTeacherParentStudentAsync(string prefix)
    {
        var teacher = await TestHelpers.CreateAuthorizedTeacherClientAsync(factory, $"{prefix}T");

        var parentEmail = TestHelpers.UniqueEmail($"{prefix}Parent");
        var parentResponse = await teacher.PostAsJsonAsync("/api/parents", new
        {
            fullName = "הורה בדיקה",
            gender = (int?)null,
            email = parentEmail,
            phone = (string?)null
        });
        parentResponse.EnsureSuccessStatusCode();
        var parentDto = await parentResponse.Content.ReadFromJsonAsync<ParentDto>();

        var studentEmail = TestHelpers.UniqueEmail($"{prefix}Student");
        var studentResponse = await teacher.PostAsJsonAsync("/api/students", new
        {
            fullName = "תלמיד בדיקה",
            gender = (int?)null,
            gradeLevel = (string?)null,
            birthDate = (string?)null,
            generalInfo = (string?)null,
            loginEmail = studentEmail,
            parentIds = new[] { parentDto!.Id }
        });
        studentResponse.EnsureSuccessStatusCode();
        var studentDto = await studentResponse.Content.ReadFromJsonAsync<StudentDto>();

        var parentClient = await LogInAsInvitedUserAsync(parentEmail, "ParentPass123");
        var studentClient = await LogInAsInvitedUserAsync(studentEmail, "StudentPass123");

        return (teacher, parentClient, studentClient, studentDto!.Id);
    }

    /// <summary>קובעת סיסמה ישירות דרך UserManager למשתמש מוזמן (הורה/תלמיד) ומחזירה קליינט מחובר.</summary>
    private async Task<HttpClient> LogInAsInvitedUserAsync(string email, string password)
    {
        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(email) ?? throw new InvalidOperationException($"Invited user {email} not found.");
        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        var result = await userManager.ResetPasswordAsync(user, token, password);
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));
        user.EmailConfirmed = true;
        await userManager.UpdateAsync(user);

        var anon = factory.CreateClient();
        var accessToken = await TestHelpers.LoginAsync(anon, email, password);
        return TestHelpers.AuthorizedClient(factory, accessToken);
    }

    private record ParentDto(Guid Id, string FullName);
    private record StudentDto(Guid Id, string FullName);
    private record NoteDto(Guid Id, Guid StudentId, string StudentName, Guid? LessonId, string Content, bool VisibleToStudent, bool VisibleToParent, DateTimeOffset CreatedAt);
    private record ParentNoteDto(Guid Id, Guid StudentId, string StudentName, string Content, DateTimeOffset CreatedAt);
    private record StudentNoteDto(Guid Id, string Content, DateTimeOffset CreatedAt);
}
