using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Talmidon.Api.Contracts;
using Talmidon.Domain.Enums;
using Talmidon.Infrastructure.Identity;

namespace Talmidon.Tests;

/// <summary>
/// מה שהורה רואה מהבקשות ששלח. עד לשינוי הזה — כלום: הוא יכול היה ליצור בקשה,
/// ומשם היא נעלמה מבחינתו, כולל כשנדחתה (דחייה אינה משנה דבר במסכים).
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class ParentChangeRequestViewTests(TalmidonWebApplicationFactory factory)
{
    [Fact]
    public async Task ParentSeesHerOwnRequest_WithTheAnswerOnIt()
    {
        var f = await CreateFamilyAsync("crView");
        var lessonId = await CreateLessonAsync(f.Teacher, f.StudentId);
        var requestId = await RequestRescheduleAsync(f.Parent, lessonId);

        var pending = await f.Parent.GetFromJsonAsync<List<ChangeRequestDto>>("/api/lessons/my-change-requests");
        var mine = Assert.Single(pending!, r => r.Id == requestId);
        Assert.Equal(ChangeRequestStatus.Pending, mine.Status);
        Assert.Equal("טיול בית ספר", mine.Reason);

        (await f.Teacher.PostAsync($"/api/lessons/change-requests/{requestId}/reject", null)).EnsureSuccessStatusCode();

        var answered = await f.Parent.GetFromJsonAsync<List<ChangeRequestDto>>("/api/lessons/my-change-requests");
        Assert.Equal(ChangeRequestStatus.Rejected, Assert.Single(answered!, r => r.Id == requestId).Status);
    }

    /// <summary>
    /// בקשה של הורה אחר לאותו ילד נכללת — זו אותה משפחה, וזה מה שמסביר את
    /// "כבר קיימת בקשה ממתינה לשיעור זה".
    /// </summary>
    [Fact]
    public async Task ARequestByTheOtherParentOfTheSameChildIsIncluded()
    {
        var f = await CreateFamilyAsync("crCoParent", secondParent: true);
        var lessonId = await CreateLessonAsync(f.Teacher, f.StudentId);
        var requestId = await RequestRescheduleAsync(f.SecondParent!, lessonId);

        var seen = await f.Parent.GetFromJsonAsync<List<ChangeRequestDto>>("/api/lessons/my-change-requests");

        Assert.Contains(seen!, r => r.Id == requestId);
    }

    /// <summary>הבקשות נושאות שם ילד וסיבה שנכתבה — הורה של משפחה אחרת אינו רואה אותן.</summary>
    [Fact]
    public async Task AnotherTeachersFamilyDoesNotSeeThem()
    {
        var a = await CreateFamilyAsync("crFamA");
        var b = await CreateFamilyAsync("crFamB");
        var lessonId = await CreateLessonAsync(a.Teacher, a.StudentId);
        var requestId = await RequestRescheduleAsync(a.Parent, lessonId);

        var seen = await b.Parent.GetFromJsonAsync<List<ChangeRequestDto>>("/api/lessons/my-change-requests");

        Assert.DoesNotContain(seen!, r => r.Id == requestId);
    }

    /// <summary>
    /// המקרה שבו מסנן הדייר לבדו אינו מספיק: שתי משפחות אצל אותה מורה. מה שחוסם
    /// כאן הוא שיוך הילד להורה, ולכן זו הבדיקה שבאמת בודקת את הסינון שנוסף.
    /// </summary>
    [Fact]
    public async Task AnotherFamilyOfTheSameTeacherDoesNotSeeThem()
    {
        var a = await CreateFamilyAsync("crSameT");
        var stranger = await AddFamilyToAsync(a.Teacher, "crSameTB");
        var lessonId = await CreateLessonAsync(a.Teacher, a.StudentId);
        var requestId = await RequestRescheduleAsync(a.Parent, lessonId);

        var seen = await stranger.GetFromJsonAsync<List<ChangeRequestDto>>("/api/lessons/my-change-requests");

        Assert.Empty(seen!);
    }

    // ----- עזר -----

    private sealed record Family(HttpClient Teacher, HttpClient Parent, HttpClient? SecondParent, Guid StudentId);

    private static async Task<Guid> CreateLessonAsync(HttpClient teacher, Guid studentId)
    {
        var start = DateTimeOffset.UtcNow.AddDays(3);
        var response = await teacher.PostAsJsonAsync("/api/lessons", new { studentId, startTime = start, endTime = start.AddHours(1) });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<LessonDto>())!.Id;
    }

    private static async Task<Guid> RequestRescheduleAsync(HttpClient parent, Guid lessonId)
    {
        var start = DateTimeOffset.UtcNow.AddDays(5);
        var response = await parent.PostAsJsonAsync($"/api/lessons/{lessonId}/change-requests", new
        {
            type = 1,
            proposedStartTime = start,
            proposedEndTime = start.AddHours(1),
            reason = "טיול בית ספר"
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ChangeRequestIdDto>())!.Id;
    }

    /// <summary>הורה וילד נוספים אצל מורה קיימת. מחזירה קליינט מחובר של אותו הורה.</summary>
    private async Task<HttpClient> AddFamilyToAsync(HttpClient teacher, string prefix)
    {
        var email = TestHelpers.UniqueEmail($"{prefix}P");
        var parentResponse = await teacher.PostAsJsonAsync("/api/parents", new
        {
            fullName = "הורה זר",
            gender = (int?)null,
            email,
            phone = (string?)null
        });
        parentResponse.EnsureSuccessStatusCode();
        var parentId = (await parentResponse.Content.ReadFromJsonAsync<ParentIdDto>())!.Id;

        var studentResponse = await teacher.PostAsJsonAsync("/api/students", new
        {
            fullName = "ילד אחר",
            gender = (int?)null,
            gradeLevel = (string?)null,
            birthDate = (string?)null,
            generalInfo = (string?)null,
            loginEmail = (string?)null,
            parentIds = new[] { parentId }
        });
        studentResponse.EnsureSuccessStatusCode();

        return await SignInAsync(email, "ParentPass123");
    }

    private async Task<Family> CreateFamilyAsync(string prefix, bool secondParent = false)
    {
        var teacher = await TestHelpers.CreateAuthorizedTeacherClientAsync(factory, $"{prefix}T");

        var parentIds = new List<Guid>();
        var emails = new List<string>();
        foreach (var suffix in secondParent ? new[] { "P1", "P2" } : new[] { "P1" })
        {
            var email = TestHelpers.UniqueEmail($"{prefix}{suffix}");
            var response = await teacher.PostAsJsonAsync("/api/parents", new
            {
                fullName = $"הורה {suffix}",
                gender = (int?)null,
                email,
                phone = (string?)null
            });
            response.EnsureSuccessStatusCode();
            parentIds.Add((await response.Content.ReadFromJsonAsync<ParentIdDto>())!.Id);
            emails.Add(email);
        }

        var studentResponse = await teacher.PostAsJsonAsync("/api/students", new
        {
            fullName = "ילד בדיקה",
            gender = (int?)null,
            gradeLevel = (string?)null,
            birthDate = (string?)null,
            generalInfo = (string?)null,
            loginEmail = (string?)null,
            parentIds = parentIds.ToArray()
        });
        studentResponse.EnsureSuccessStatusCode();
        var student = await studentResponse.Content.ReadFromJsonAsync<StudentDetailDto>();

        var first = await SignInAsync(emails[0], "ParentPass123");
        var second = secondParent ? await SignInAsync(emails[1], "ParentPass123") : null;
        return new Family(teacher, first, second, student!.Id);
    }

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

    private record ParentIdDto(Guid Id);
    private record ChangeRequestIdDto(Guid Id);
}
