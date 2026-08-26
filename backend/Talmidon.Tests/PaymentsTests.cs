using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Talmidon.Infrastructure.Identity;

namespace Talmidon.Tests;

/// <summary>
/// תשלומים (PaymentsController) לא היו מכוסים בכלל. מכסה את השומרים העסקיים המרכזיים ב-Create
/// (שיעור שלא מסומן לתשלום, ושיעור ששייך לילד של הורה אחר — לא ל"הורה המשלם" שנבחר), ואת
/// המסלול החיובי המלא: סימון שיעור פתוח כ"שולם" מוציא אותו מרשימת החיובים הפתוחים, ומחיקת
/// התשלום בטעות מחזירה אותו לרשימה. גם מוודא שהורה רואה רק את החיובים הפתוחים של ילדיו-שלו.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class PaymentsTests(TalmidonWebApplicationFactory factory)
{
    [Fact]
    public async Task Create_ForLessonBelongingToADifferentParentsChild_ReturnsBadRequest()
    {
        var teacher = await TestHelpers.CreateAuthorizedTeacherClientAsync(factory, "payCrossT");
        var (_, studentAId) = await CreateParentWithChildAsync(teacher, "payCrossParentA");
        await CreateParentWithChildAsync(teacher, "payCrossParentB");
        var lessonId = await CreateCompletedPayableLessonAsync(teacher, studentAId, amount: 100);

        var parentBDto = await GetParentDtoAsync(teacher, "payCrossParentB");

        var response = await teacher.PostAsJsonAsync("/api/payments", new
        {
            parentId = parentBDto.Id,
            lessonIds = new[] { lessonId },
            paidDate = DateOnly.FromDateTime(DateTime.UtcNow),
            method = (string?)null,
            note = (string?)null
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_ForLessonNotMarkedPaymentRequired_ReturnsConflict()
    {
        var teacher = await TestHelpers.CreateAuthorizedTeacherClientAsync(factory, "payNoChargeT");
        var (_, parentId, studentId) = await CreateParentWithChildReturningIdsAsync(teacher, "payNoCharge");

        // שיעור מתוזמן רגיל — לא סומן מעולם כבר-חיוב.
        var lessonResponse = await teacher.PostAsJsonAsync("/api/lessons", new
        {
            studentId,
            startTime = DateTimeOffset.UtcNow.AddDays(1),
            endTime = DateTimeOffset.UtcNow.AddDays(1).AddMinutes(45),
            reason = (string?)null
        });
        lessonResponse.EnsureSuccessStatusCode();
        var lesson = await lessonResponse.Content.ReadFromJsonAsync<LessonDto>();

        var response = await teacher.PostAsJsonAsync("/api/payments", new
        {
            parentId,
            lessonIds = new[] { lesson!.Id },
            paidDate = DateOnly.FromDateTime(DateTime.UtcNow),
            method = (string?)null,
            note = (string?)null
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Create_MarksLessonPaid_AndRemovesItFromOpenCharges()
    {
        var teacher = await TestHelpers.CreateAuthorizedTeacherClientAsync(factory, "payHappyT");
        var (_, parentId, studentId) = await CreateParentWithChildReturningIdsAsync(teacher, "payHappy");
        var lessonId = await CreateCompletedPayableLessonAsync(teacher, studentId, amount: 150);

        var beforeCharges = await teacher.GetFromJsonAsync<List<OpenChargeDto>>($"/api/payments/open-charges?studentId={studentId}");
        Assert.Contains(beforeCharges!, c => c.LessonId == lessonId);

        var paymentResponse = await teacher.PostAsJsonAsync("/api/payments", new
        {
            parentId,
            lessonIds = new[] { lessonId },
            paidDate = DateOnly.FromDateTime(DateTime.UtcNow),
            method = "מזומן",
            note = (string?)null
        });
        paymentResponse.EnsureSuccessStatusCode();
        var payment = await paymentResponse.Content.ReadFromJsonAsync<PaymentDto>();

        Assert.Equal(150, payment!.Amount);
        Assert.Equal(1, payment.LessonCount);

        var afterCharges = await teacher.GetFromJsonAsync<List<OpenChargeDto>>($"/api/payments/open-charges?studentId={studentId}");
        Assert.DoesNotContain(afterCharges!, c => c.LessonId == lessonId);
    }

    [Fact]
    public async Task Delete_ReopensTheLessonAsAnUnpaidCharge()
    {
        var teacher = await TestHelpers.CreateAuthorizedTeacherClientAsync(factory, "payDeleteT");
        var (_, parentId, studentId) = await CreateParentWithChildReturningIdsAsync(teacher, "payDelete");
        var lessonId = await CreateCompletedPayableLessonAsync(teacher, studentId, amount: 200);

        var paymentResponse = await teacher.PostAsJsonAsync("/api/payments", new
        {
            parentId,
            lessonIds = new[] { lessonId },
            paidDate = DateOnly.FromDateTime(DateTime.UtcNow),
            method = (string?)null,
            note = (string?)null
        });
        paymentResponse.EnsureSuccessStatusCode();
        var payment = await paymentResponse.Content.ReadFromJsonAsync<PaymentDto>();

        var deleteResponse = await teacher.DeleteAsync($"/api/payments/{payment!.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var chargesAfterDelete = await teacher.GetFromJsonAsync<List<OpenChargeDto>>($"/api/payments/open-charges?studentId={studentId}");
        Assert.Contains(chargesAfterDelete!, c => c.LessonId == lessonId);
    }

    [Fact]
    public async Task MyOpenCharges_OnlyReturnsTheRequestingParentsOwnChildren()
    {
        var teacher = await TestHelpers.CreateAuthorizedTeacherClientAsync(factory, "payScopeT");
        var (parentAClient, studentAId) = await CreateParentWithChildAsync(teacher, "payScopeA");
        var (_, studentBId) = await CreateParentWithChildAsync(teacher, "payScopeB");
        var lessonAId = await CreateCompletedPayableLessonAsync(teacher, studentAId, amount: 80);
        await CreateCompletedPayableLessonAsync(teacher, studentBId, amount: 90);

        var charges = await parentAClient.GetFromJsonAsync<List<OpenChargeDto>>("/api/payments/mine/open-charges");

        Assert.Single(charges!);
        Assert.Equal(lessonAId, charges![0].LessonId);
    }

    // ----- עזר -----

    private static async Task<Guid> CreateCompletedPayableLessonAsync(HttpClient teacherClient, Guid studentId, decimal amount)
    {
        var lessonResponse = await teacherClient.PostAsJsonAsync("/api/lessons", new
        {
            studentId,
            startTime = DateTimeOffset.UtcNow.AddDays(-1),
            endTime = DateTimeOffset.UtcNow.AddDays(-1).AddMinutes(45),
            reason = (string?)null
        });
        lessonResponse.EnsureSuccessStatusCode();
        var lesson = await lessonResponse.Content.ReadFromJsonAsync<LessonDto>();

        var completeResponse = await teacherClient.PostAsJsonAsync($"/api/lessons/{lesson!.Id}/complete", new
        {
            completed = true,
            paymentRequired = true,
            amount,
            homework = (string?)null,
            noteContent = (string?)null,
            noteVisibleToStudent = false,
            noteVisibleToParent = false
        });
        completeResponse.EnsureSuccessStatusCode();

        return lesson.Id;
    }

    private async Task<ParentDto> GetParentDtoAsync(HttpClient teacherClient, string prefixUsedAsFullName)
    {
        var parents = await teacherClient.GetFromJsonAsync<List<ParentDto>>("/api/parents");
        return parents!.First(p => p.FullName == prefixUsedAsFullName);
    }

    private Task<(HttpClient ParentClient, Guid StudentId)> CreateParentWithChildAsync(HttpClient teacherClient, string parentFullName) =>
        CreateParentWithChildInternalAsync(teacherClient, parentFullName);

    private async Task<(HttpClient ParentClient, Guid ParentId, Guid StudentId)> CreateParentWithChildReturningIdsAsync(HttpClient teacherClient, string prefix)
    {
        var parentFullName = $"{prefix}Parent";
        var (parentClient, studentId) = await CreateParentWithChildInternalAsync(teacherClient, parentFullName);
        var parentDto = await GetParentDtoAsync(teacherClient, parentFullName);
        return (parentClient, parentDto.Id, studentId);
    }

    private async Task<(HttpClient ParentClient, Guid StudentId)> CreateParentWithChildInternalAsync(HttpClient teacherClient, string parentFullName)
    {
        var parentEmail = TestHelpers.UniqueEmail("payParent");
        var parentResponse = await teacherClient.PostAsJsonAsync("/api/parents", new
        {
            fullName = parentFullName,
            gender = (int?)null,
            email = parentEmail,
            phone = (string?)null
        });
        parentResponse.EnsureSuccessStatusCode();
        var parent = await parentResponse.Content.ReadFromJsonAsync<ParentDto>();

        var studentResponse = await teacherClient.PostAsJsonAsync("/api/students", new
        {
            fullName = $"ילד/ה של {parentFullName}",
            gender = (int?)null,
            gradeLevel = (string?)null,
            birthDate = (string?)null,
            generalInfo = (string?)null,
            loginEmail = (string?)null,
            parentIds = new[] { parent!.Id }
        });
        studentResponse.EnsureSuccessStatusCode();
        var student = await studentResponse.Content.ReadFromJsonAsync<StudentDto>();

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

        return (parentClient, student!.Id);
    }

    private record ParentDto(Guid Id, string FullName);
    private record StudentDto(Guid Id, string FullName);
    private record LessonDto(Guid Id);
    private record OpenChargeDto(Guid LessonId, Guid StudentId, string StudentName, DateTimeOffset LessonStartTime, decimal Amount);
    private record PaymentDto(Guid Id, Guid ParentId, string ParentName, decimal Amount, DateOnly PaidDate, string? Method, string? Note, int LessonCount, DateTimeOffset? ConfirmationSentAt);
}
