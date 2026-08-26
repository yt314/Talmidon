using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Talmidon.Domain.Enums;
using Talmidon.Infrastructure.Identity;

namespace Talmidon.Tests;

/// <summary>
/// שומרי מצב (guards) על מחזור החיים של שיעור ובקשות שינוי: אי אפשר לעדכן/למחוק/לסיים שיעור
/// שאינו במצב הנכון, אי אפשר לאשר/לדחות בקשה שכבר טופלה, ואי אפשר לפתוח שתי בקשות שינוי
/// ממתינות לאותו שיעור בו-זמנית. גם המסלולים החיוביים (אישור ביטול/שינוי מועד) מוודאים
/// שהאפקט בפועל על השיעור נכון, לא רק שהבקשה עצמה מסומנת "אושרה".
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class LessonStatusTransitionTests(TalmidonWebApplicationFactory factory)
{
    [Fact]
    public async Task Update_WhenLessonNotScheduled_ReturnsConflict()
    {
        var (teacher, _, studentId) = await CreateTeacherWithParentAndStudentAsync("txUpdate");
        var lessonId = await CreateScheduledLessonAsync(teacher, studentId);
        await CompleteLessonAsync(teacher, lessonId);

        var response = await teacher.PutAsJsonAsync($"/api/lessons/{lessonId}", new
        {
            startTime = DateTimeOffset.UtcNow.AddDays(3),
            endTime = DateTimeOffset.UtcNow.AddDays(3).AddMinutes(45)
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Delete_WhenLessonCompleted_ReturnsConflict()
    {
        var (teacher, _, studentId) = await CreateTeacherWithParentAndStudentAsync("txDelete");
        var lessonId = await CreateScheduledLessonAsync(teacher, studentId);
        await CompleteLessonAsync(teacher, lessonId);

        var response = await teacher.DeleteAsync($"/api/lessons/{lessonId}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Complete_WhenLessonAlreadyCompleted_ReturnsConflict()
    {
        var (teacher, _, studentId) = await CreateTeacherWithParentAndStudentAsync("txComplete");
        var lessonId = await CreateScheduledLessonAsync(teacher, studentId);
        await CompleteLessonAsync(teacher, lessonId);

        var response = await teacher.PostAsJsonAsync($"/api/lessons/{lessonId}/complete", new
        {
            completed = true,
            paymentRequired = false,
            amount = 0,
            homework = (string?)null,
            noteContent = (string?)null,
            noteVisibleToStudent = false,
            noteVisibleToParent = false
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task ApproveRequest_WhenLessonAlreadyScheduled_ReturnsConflict()
    {
        // שיעור שנקבע ע"י המורה נכנס ישר כ-Scheduled, לא Requested — אישור עליו לא אמור להתקבל.
        var (teacher, _, studentId) = await CreateTeacherWithParentAndStudentAsync("txApprove");
        var lessonId = await CreateScheduledLessonAsync(teacher, studentId);

        var response = await teacher.PostAsync($"/api/lessons/{lessonId}/approve", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task DeclineRequest_WhenLessonAlreadyScheduled_ReturnsConflict()
    {
        var (teacher, _, studentId) = await CreateTeacherWithParentAndStudentAsync("txDecline");
        var lessonId = await CreateScheduledLessonAsync(teacher, studentId);

        var response = await teacher.PostAsync($"/api/lessons/{lessonId}/decline", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task RequestChange_WhenLessonStillPendingApproval_ReturnsConflict()
    {
        var (_, parent, studentId) = await CreateTeacherWithParentAndStudentAsync("txReqChange");
        var requestResponse = await parent.PostAsJsonAsync("/api/lessons/requests", new
        {
            studentId,
            startTime = DateTimeOffset.UtcNow.AddDays(1),
            endTime = DateTimeOffset.UtcNow.AddDays(1).AddMinutes(45),
            reason = (string?)null
        });
        requestResponse.EnsureSuccessStatusCode();
        var lesson = await requestResponse.Content.ReadFromJsonAsync<LessonDto>();

        var response = await parent.PostAsJsonAsync($"/api/lessons/{lesson!.Id}/change-requests", new
        {
            type = 0, // Cancel
            proposedStartTime = (DateTimeOffset?)null,
            proposedEndTime = (DateTimeOffset?)null,
            reason = (string?)null
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task RequestChange_WhenPendingRequestAlreadyExists_ReturnsConflict()
    {
        var (teacher, parent, studentId) = await CreateTeacherWithParentAndStudentAsync("txDupChange");
        var lessonId = await CreateScheduledLessonAsync(teacher, studentId);
        var first = await RequestCancelAsync(parent, lessonId);
        first.EnsureSuccessStatusCode();

        var second = await RequestCancelAsync(parent, lessonId);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task ApproveChangeRequest_Cancel_ActuallyCancelsTheLesson()
    {
        var (teacher, parent, studentId) = await CreateTeacherWithParentAndStudentAsync("txApproveCancel");
        var lessonId = await CreateScheduledLessonAsync(teacher, studentId);
        var changeRequestId = await RequestCancelAndGetIdAsync(parent, lessonId);

        var approve = await teacher.PostAsync($"/api/lessons/change-requests/{changeRequestId}/approve", null);
        Assert.Equal(HttpStatusCode.NoContent, approve.StatusCode);

        var lesson = await teacher.GetFromJsonAsync<LessonDto>($"/api/lessons/{lessonId}");
        Assert.Equal(LessonStatus.Cancelled, lesson!.Status);
    }

    [Fact]
    public async Task ApproveChangeRequest_Reschedule_ActuallyMovesTheLesson()
    {
        var (teacher, parent, studentId) = await CreateTeacherWithParentAndStudentAsync("txApproveReschedule");
        var lessonId = await CreateScheduledLessonAsync(teacher, studentId);

        var proposedStart = DateTimeOffset.UtcNow.AddDays(10);
        var proposedEnd = proposedStart.AddMinutes(45);
        var changeResponse = await parent.PostAsJsonAsync($"/api/lessons/{lessonId}/change-requests", new
        {
            type = 1, // Reschedule
            proposedStartTime = proposedStart,
            proposedEndTime = proposedEnd,
            reason = (string?)null
        });
        changeResponse.EnsureSuccessStatusCode();
        var changeRequest = await changeResponse.Content.ReadFromJsonAsync<ChangeRequestDto>();

        var approve = await teacher.PostAsync($"/api/lessons/change-requests/{changeRequest!.Id}/approve", null);
        Assert.Equal(HttpStatusCode.NoContent, approve.StatusCode);

        // Postgres מאחסן timestamptz במיקרו-שנייה בעוד DateTimeOffset מדייק לטיק (100ns) — משווים
        // בסבילות קטנה כדי לא להיכשל על עיגול תת-מיקרו-שנייתי שאין לו שום משמעות עסקית.
        var lesson = await teacher.GetFromJsonAsync<LessonDto>($"/api/lessons/{lessonId}");
        Assert.Equal(proposedStart, lesson!.StartTime, TimeSpan.FromMilliseconds(1));
        Assert.Equal(proposedEnd, lesson.EndTime, TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public async Task ApproveChangeRequest_WhenAlreadyResolved_ReturnsConflict()
    {
        var (teacher, parent, studentId) = await CreateTeacherWithParentAndStudentAsync("txReapprove");
        var lessonId = await CreateScheduledLessonAsync(teacher, studentId);
        var changeRequestId = await RequestCancelAndGetIdAsync(parent, lessonId);
        (await teacher.PostAsync($"/api/lessons/change-requests/{changeRequestId}/approve", null)).EnsureSuccessStatusCode();

        var secondApprove = await teacher.PostAsync($"/api/lessons/change-requests/{changeRequestId}/approve", null);

        Assert.Equal(HttpStatusCode.Conflict, secondApprove.StatusCode);
    }

    [Fact]
    public async Task RejectChangeRequest_WhenAlreadyResolved_ReturnsConflict()
    {
        var (teacher, parent, studentId) = await CreateTeacherWithParentAndStudentAsync("txReject");
        var lessonId = await CreateScheduledLessonAsync(teacher, studentId);
        var changeRequestId = await RequestCancelAndGetIdAsync(parent, lessonId);
        (await teacher.PostAsync($"/api/lessons/change-requests/{changeRequestId}/reject", null)).EnsureSuccessStatusCode();

        var secondReject = await teacher.PostAsync($"/api/lessons/change-requests/{changeRequestId}/reject", null);

        Assert.Equal(HttpStatusCode.Conflict, secondReject.StatusCode);
    }

    // ----- עזר -----

    private async Task<Guid> CreateScheduledLessonAsync(HttpClient teacherClient, Guid studentId)
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

    private static Task<HttpResponseMessage> CompleteLessonAsync(HttpClient teacherClient, Guid lessonId) =>
        teacherClient.PostAsJsonAsync($"/api/lessons/{lessonId}/complete", new
        {
            completed = true,
            paymentRequired = false,
            amount = 0,
            homework = (string?)null,
            noteContent = (string?)null,
            noteVisibleToStudent = false,
            noteVisibleToParent = false
        });

    private static Task<HttpResponseMessage> RequestCancelAsync(HttpClient parentClient, Guid lessonId) =>
        parentClient.PostAsJsonAsync($"/api/lessons/{lessonId}/change-requests", new
        {
            type = 0, // Cancel
            proposedStartTime = (DateTimeOffset?)null,
            proposedEndTime = (DateTimeOffset?)null,
            reason = (string?)null
        });

    private static async Task<Guid> RequestCancelAndGetIdAsync(HttpClient parentClient, Guid lessonId)
    {
        var response = await RequestCancelAsync(parentClient, lessonId);
        response.EnsureSuccessStatusCode();
        var changeRequest = await response.Content.ReadFromJsonAsync<ChangeRequestDto>();
        return changeRequest!.Id;
    }

    private async Task<(HttpClient Teacher, HttpClient Parent, Guid StudentId)> CreateTeacherWithParentAndStudentAsync(string prefix)
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

        var studentResponse = await teacher.PostAsJsonAsync("/api/students", new
        {
            fullName = "תלמיד בדיקה",
            gender = (int?)null,
            gradeLevel = (string?)null,
            birthDate = (string?)null,
            generalInfo = (string?)null,
            loginEmail = (string?)null,
            parentIds = new[] { parentDto!.Id }
        });
        studentResponse.EnsureSuccessStatusCode();
        var studentDto = await studentResponse.Content.ReadFromJsonAsync<StudentDto>();

        const string password = "ParentPass123";
        using (var scope = factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByEmailAsync(parentEmail) ?? throw new InvalidOperationException("Parent user not found.");
            var token = await userManager.GeneratePasswordResetTokenAsync(user);
            var result = await userManager.ResetPasswordAsync(user, token, password);
            if (!result.Succeeded)
                throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));
            user.EmailConfirmed = true;
            await userManager.UpdateAsync(user);
        }

        var anon = factory.CreateClient();
        var accessToken = await TestHelpers.LoginAsync(anon, parentEmail, password);
        var parentClient = TestHelpers.AuthorizedClient(factory, accessToken);

        return (teacher, parentClient, studentDto!.Id);
    }

    private record ParentDto(Guid Id, string FullName);
    private record StudentDto(Guid Id, string FullName);
    private record LessonDto(Guid Id, Guid StudentId, string StudentName, DateTimeOffset StartTime, DateTimeOffset EndTime, LessonStatus Status);
    private record ChangeRequestDto(Guid Id, Guid LessonId);
}
