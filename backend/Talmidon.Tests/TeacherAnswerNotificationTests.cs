using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Talmidon.Api.Contracts;
using Talmidon.Infrastructure.Email;
using Talmidon.Infrastructure.Identity;

namespace Talmidon.Tests;

/// <summary>
/// שלוש החלטות של המורה שמשנות משהו למשפחה ולא סיפרו לאיש: אישור בקשת שינוי,
/// דחייתה, וסימון "לא הגיע".
///
/// דחייה היא המקרה החד ביותר — היא אינה משנה דבר במסכים (השיעור נשאר במועדו
/// והבקשה יורדת מרשימת הממתינות), כך שההורה ששאל פשוט לא קיבל תשובה.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class TeacherAnswerNotificationTests(TalmidonWebApplicationFactory factory)
{
    private const string StudentName = "תלמידה בבדיקה";

    [Fact]
    public async Task RejectingAChangeRequest_TellsTheParentTheLessonStands()
    {
        var f = await CreateFamilyAsync("rejChange");
        var lessonId = await CreateLessonAsync(f.Teacher, f.StudentId);
        var requestId = await RequestRescheduleAsync(f.Parent, lessonId);

        (await f.Teacher.PostAsync($"/api/lessons/change-requests/{requestId}/reject", null)).EnsureSuccessStatusCode();

        var mail = Assert.Single(factory.SentEmails.To(f.ParentEmail), e => e.Subject.StartsWith("הבקשה לשינוי השיעור"));
        Assert.Contains("לא אושרה", mail.Subject);
        Assert.Contains("מתקיים כמתוכנן", mail.HtmlBody);
    }

    [Fact]
    public async Task ApprovingAReschedule_TellsTheParentAndTheStudentTheNewTime()
    {
        var f = await CreateFamilyAsync("appChange", withStudentLogin: true);
        var lessonId = await CreateLessonAsync(f.Teacher, f.StudentId);
        var newStart = TestHelpers.MiddayUtcIn(6);
        var requestId = await RequestRescheduleAsync(f.Parent, lessonId, newStart);

        (await f.Teacher.PostAsync($"/api/lessons/change-requests/{requestId}/approve", null)).EnsureSuccessStatusCode();

        var toParent = Assert.Single(factory.SentEmails.To(f.ParentEmail), e => e.Subject.Contains("עודכן"));
        // Israel time, like the subject itself — see LessonRequestAnswerTests.
        Assert.Contains(EmailTime.Date(newStart), toParent.Subject);
        // היומן שהשתנה הוא של התלמידה, ולכן גם היא מקבלת
        Assert.Contains(factory.SentEmails.To(f.StudentEmail), e => e.Subject.Contains("עודכן"));
    }

    [Fact]
    public async Task ApprovingACancellation_TellsThemTheLessonIsOff()
    {
        var f = await CreateFamilyAsync("appCancel");
        var lessonId = await CreateLessonAsync(f.Teacher, f.StudentId);
        var requestId = await RequestCancelAsync(f.Parent, lessonId);

        (await f.Teacher.PostAsync($"/api/lessons/change-requests/{requestId}/approve", null)).EnsureSuccessStatusCode();

        var mail = Assert.Single(factory.SentEmails.To(f.ParentEmail), e => e.Subject.Contains("בוטל"));
        Assert.Contains("בקשת הביטול אושרה", mail.HtmlBody);
    }

    /// <summary>
    /// "לא הגיע" מוריד את השיעור מהמסכים בלי חיוב. ההורה הוא היחיד שאינו יודע
    /// אם הילד/ה היה/הייתה שם — וזו בדיוק ההודעה שהוא צריך לקבל מיד.
    /// </summary>
    [Fact]
    public async Task MarkingNoShow_TellsTheParent()
    {
        var f = await CreateFamilyAsync("noShow");
        var lessonId = await CreateLessonAsync(f.Teacher, f.StudentId);

        (await f.Teacher.PostAsync($"/api/lessons/{lessonId}/no-show", null)).EnsureSuccessStatusCode();

        var mail = Assert.Single(factory.SentEmails.To(f.ParentEmail), e => e.Subject.Contains("לא הגיע/ה"));
        Assert.Contains(StudentName, mail.Subject);
        Assert.Contains("לא חויב", mail.HtmlBody);
    }

    // ----- עזר -----

    private sealed record Family(
        HttpClient Teacher, HttpClient Parent, Guid StudentId, string ParentEmail, string StudentEmail);

    private static async Task<Guid> CreateLessonAsync(HttpClient teacher, Guid studentId)
    {
        var start = DateTimeOffset.UtcNow.AddDays(3);
        var response = await teacher.PostAsJsonAsync("/api/lessons", new
        {
            studentId,
            startTime = start,
            endTime = start.AddHours(1)
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<LessonDto>())!.Id;
    }

    private static async Task<Guid> RequestRescheduleAsync(HttpClient parent, Guid lessonId, DateTimeOffset? newStart = null)
    {
        var start = newStart ?? DateTimeOffset.UtcNow.AddDays(5);
        var response = await parent.PostAsJsonAsync($"/api/lessons/{lessonId}/change-requests", new
        {
            type = 1, // Reschedule
            proposedStartTime = start,
            proposedEndTime = start.AddHours(1),
            reason = "טיול בית ספר"
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ChangeRequestDto>())!.Id;
    }

    private static async Task<Guid> RequestCancelAsync(HttpClient parent, Guid lessonId)
    {
        var response = await parent.PostAsJsonAsync($"/api/lessons/{lessonId}/change-requests", new
        {
            type = 0, // Cancel
            proposedStartTime = (DateTimeOffset?)null,
            proposedEndTime = (DateTimeOffset?)null,
            reason = (string?)null
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ChangeRequestDto>())!.Id;
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
        var parent = await parentResponse.Content.ReadFromJsonAsync<ParentDto>();

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

        var parentClient = await SignInAsync(parentEmail, "ParentPass123");
        return new Family(teacher, parentClient, student!.Id, parentEmail, studentEmail ?? "");
    }

    /// <summary>קובעת סיסמה לחשבון שנוצר ע"י המורה ומתחברת בו — אין מייל הזמנה אמיתי בבדיקות.</summary>
    private async Task<HttpClient> SignInAsync(string email, string password)
    {
        using (var scope = factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByEmailAsync(email)
                ?? throw new InvalidOperationException($"User {email} not found.");
            var token = await userManager.GeneratePasswordResetTokenAsync(user);
            var result = await userManager.ResetPasswordAsync(user, token, password);
            if (!result.Succeeded)
                throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));
            user.EmailConfirmed = true;
            await userManager.UpdateAsync(user);
        }

        var accessToken = await TestHelpers.LoginAsync(factory.CreateClient(), email, password);
        return TestHelpers.AuthorizedClient(factory, accessToken);
    }

    private record ParentDto(Guid Id, string FullName);
    private record ChangeRequestDto(Guid Id, Guid LessonId);
}
