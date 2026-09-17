using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Talmidon.Api.Contracts;
using Talmidon.Domain.Enums;
using Talmidon.Infrastructure.Identity;

namespace Talmidon.Tests;

/// <summary>
/// התשובה של המורה לבקשת שיעור — אישור או דחייה — צריכה להגיע למי שביקשה.
///
/// דחייה הורידה את השיעור מכל רשימות "השיעורים הקרובים" ולא הודיעה לאיש, כך שתלמידה
/// שביקשה מועד ראתה בקשה ממתינה ואז כלום: בלי לדעת אם נדחתה או שהבקשה לא נקלטה.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class LessonRequestAnswerTests(TalmidonWebApplicationFactory factory)
{
    private const string StudentName = "תלמידה מבקשת";

    /// <summary>
    /// תחילת שורת הנושא, ולא חיפוש מילה: "אושרה" מוכלת גם ב"לא אושרה", ובדיקה על המילה
    /// לבדה הייתה עוברת גם אם המערכת שולחת בדיוק את ההפך ממה שקרה.
    /// </summary>
    private const string Approved = $"בקשת השיעור של {StudentName} אושרה";
    private const string Declined = $"בקשת השיעור של {StudentName} לא אושרה";

    [Fact]
    public async Task Decline_EmailsBothTheStudentAndHerParent()
    {
        var family = await CreateFamilyAsync("declineMail");
        var lessonId = await RequestLessonAsync(family.Student);

        var response = await family.Teacher.PostAsync($"/api/lessons/{lessonId}/decline", null);
        response.EnsureSuccessStatusCode();

        Assert.Contains(factory.SentEmails.To(family.StudentEmail), e => e.Subject.StartsWith(Declined));
        Assert.Contains(factory.SentEmails.To(family.ParentEmail), e => e.Subject.StartsWith(Declined));
    }

    /// <summary>המייל צריך לומר על איזה מועד מדובר — "הבקשה שלך נדחתה" בלי תאריך אינו תשובה.</summary>
    [Fact]
    public async Task Decline_EmailNamesTheDateThatWasRequested()
    {
        var family = await CreateFamilyAsync("declineDate");
        var start = DateTimeOffset.UtcNow.AddDays(5);
        var lessonId = await RequestLessonAsync(family.Student, start);

        (await family.Teacher.PostAsync($"/api/lessons/{lessonId}/decline", null)).EnsureSuccessStatusCode();

        var answer = Assert.Single(factory.SentEmails.To(family.StudentEmail), e => e.Subject.StartsWith(Declined));
        Assert.Contains(start.ToString("dd/MM/yyyy"), answer.Subject);
    }

    [Fact]
    public async Task Approve_EmailsBothTheStudentAndHerParent()
    {
        var family = await CreateFamilyAsync("approveMail");
        var lessonId = await RequestLessonAsync(family.Student);

        var response = await family.Teacher.PostAsync($"/api/lessons/{lessonId}/approve", null);
        response.EnsureSuccessStatusCode();

        Assert.Contains(factory.SentEmails.To(family.StudentEmail), e => e.Subject.StartsWith(Approved));
        Assert.Contains(factory.SentEmails.To(family.ParentEmail), e => e.Subject.StartsWith(Approved));
    }

    /// <summary>
    /// הבקשה שנדחתה נשארת ביומן של התלמידה עם הסטטוס "נדחה" — זה מה שמסכי הפורטל
    /// מציגים בכרטיס "בקשות שלא אושרו". אם היא הייתה נעלמת מהשרת, לא היה מה להציג.
    /// </summary>
    [Fact]
    public async Task Decline_LeavesTheRequestVisibleInHerSchedule()
    {
        var family = await CreateFamilyAsync("declineVisible");
        var lessonId = await RequestLessonAsync(family.Student);

        (await family.Teacher.PostAsync($"/api/lessons/{lessonId}/decline", null)).EnsureSuccessStatusCode();

        var schedule = await family.Student.GetFromJsonAsync<List<StudentLessonDto>>("/api/lessons/my-schedule");
        Assert.Contains(schedule!, l => l.Id == lessonId && l.Status == LessonStatus.Declined);
    }

    /// <summary>תלמידה צעירה בלי חשבון התחברות — ההורה שביקש עדיין מקבל תשובה.</summary>
    [Fact]
    public async Task Decline_WhenTheStudentHasNoLogin_StillReachesTheParent()
    {
        var family = await CreateFamilyAsync("declineNoLogin", withStudentLogin: false);
        var lessonId = await RequestLessonAsParentAsync(family.Parent, family.StudentId);

        (await family.Teacher.PostAsync($"/api/lessons/{lessonId}/decline", null)).EnsureSuccessStatusCode();

        Assert.Contains(factory.SentEmails.To(family.ParentEmail), e => e.Subject.StartsWith(Declined));
    }

    // ----- עזר -----

    private sealed record Family(
        HttpClient Teacher, HttpClient Student, HttpClient Parent, Guid StudentId, string StudentEmail, string ParentEmail);

    private async Task<Guid> RequestLessonAsync(HttpClient student, DateTimeOffset? start = null)
    {
        var from = start ?? DateTimeOffset.UtcNow.AddDays(3);
        var response = await student.PostAsJsonAsync("/api/lessons/my-requests", new
        {
            startTime = from,
            endTime = from.AddHours(1),
            reason = (string?)null
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<LessonDto>())!.Id;
    }

    /// <summary>בקשה שנפתחה ע"י ההורה. מסלול נפרד מזה של התלמידה, ונכנס גם הוא כ-Requested.</summary>
    private static async Task<Guid> RequestLessonAsParentAsync(HttpClient parent, Guid studentId)
    {
        var start = DateTimeOffset.UtcNow.AddDays(3);
        var response = await parent.PostAsJsonAsync("/api/lessons/requests", new
        {
            studentId,
            startTime = start,
            endTime = start.AddHours(1),
            reason = (string?)null
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<LessonDto>())!.Id;
    }

    private async Task<Family> CreateFamilyAsync(string prefix, bool withStudentLogin = true)
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
        var studentClient = studentEmail is null ? teacher : await SignInAsync(studentEmail, "StudentPass123");

        return new Family(teacher, studentClient, parentClient, student!.Id, studentEmail ?? "", parentEmail);
    }

    /// <summary>
    /// קובעת סיסמה לחשבון שנוצר ע"י המורה ומתחברת בו. אין כאן מייל הזמנה אמיתי,
    /// ולכן עוברים ישירות דרך UserManager — כמו בשאר הבדיקות.
    /// </summary>
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
}
