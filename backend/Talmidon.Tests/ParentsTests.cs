using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Talmidon.Infrastructure.Identity;

namespace Talmidon.Tests;

/// <summary>
/// ניהול הורים (ParentsController) — מה שלא היה מכוסה: עדכון, מחיקה שמוחקת גם את חשבון
/// הכניסה, ומחיקה שנחסמת כי יש להורה תשלומים — ואז גם החשבון חייב להישאר.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class ParentsTests(TalmidonWebApplicationFactory factory)
{
    [Fact]
    public async Task Update_ChangesTheParentsDetails_ButNotTheirEmail()
    {
        var teacher = await TestHelpers.CreateAuthorizedTeacherClientAsync(factory, "parUpdateT");
        var parent = await CreateParentAsync(teacher, "שרה");

        var update = await teacher.PutAsJsonAsync($"/api/parents/{parent.Id}",
            new { fullName = "שרה לוי", gender = (int?)null, phone = "0521112233" });
        Assert.Equal(HttpStatusCode.NoContent, update.StatusCode);

        var detail = await teacher.GetFromJsonAsync<ParentDto>($"/api/parents/{parent.Id}");
        Assert.Equal(("שרה לוי", "0521112233", parent.Email), (detail!.FullName, detail.Phone, detail.Email));
    }

    [Fact]
    public async Task Delete_RemovesTheParentAndTheirLoginAccount()
    {
        var teacher = await TestHelpers.CreateAuthorizedTeacherClientAsync(factory, "parDeleteT");
        var parent = await CreateParentAsync(teacher, "מחוקה");

        Assert.Equal(HttpStatusCode.NoContent, (await teacher.DeleteAsync($"/api/parents/{parent.Id}")).StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await teacher.GetAsync($"/api/parents/{parent.Id}")).StatusCode);
        Assert.Null(await FindUserAsync(parent.Email));
    }

    [Fact]
    public async Task Delete_OfAParentWithPayments_IsRefused_AndKeepsTheirAccount()
    {
        var teacher = await TestHelpers.CreateAuthorizedTeacherClientAsync(factory, "parPaidT");
        var parent = await CreateParentAsync(teacher, "משלמת");
        await RecordAPaymentAsync(teacher, parent.Id);

        var delete = await teacher.DeleteAsync($"/api/parents/{parent.Id}");

        Assert.Equal(HttpStatusCode.Conflict, delete.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await teacher.GetAsync($"/api/parents/{parent.Id}")).StatusCode);
        Assert.NotNull(await FindUserAsync(parent.Email));
    }

    // ----- עזר -----

    private async Task<ApplicationUser?> FindUserAsync(string email)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByEmailAsync(email);
    }

    private static async Task<ParentDto> CreateParentAsync(HttpClient teacher, string fullName)
    {
        var response = await teacher.PostAsJsonAsync("/api/parents", new
        {
            fullName, gender = (int?)null, email = TestHelpers.UniqueEmail("parent"), phone = (string?)null
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ParentDto>())!;
    }

    private static async Task RecordAPaymentAsync(HttpClient teacher, Guid parentId)
    {
        var student = await teacher.PostAsJsonAsync("/api/students", new
        {
            fullName = "ילדה", gender = (int?)null, gradeLevel = (string?)null, birthDate = (string?)null,
            generalInfo = (string?)null, loginEmail = (string?)null, parentIds = new[] { parentId }
        });
        var studentId = (await student.Content.ReadFromJsonAsync<IdDto>())!.Id;

        var start = TestHelpers.MiddayUtcIn(-2);
        var lesson = await teacher.PostAsJsonAsync("/api/lessons", new { studentId, startTime = start, endTime = start.AddMinutes(45) });
        var lessonId = (await lesson.Content.ReadFromJsonAsync<IdDto>())!.Id;
        (await teacher.PostAsJsonAsync($"/api/lessons/{lessonId}/complete", new
        {
            completed = true, paymentRequired = true, amount = 100, homework = (string?)null,
            noteContent = (string?)null, noteVisibleToStudent = false, noteVisibleToParent = false
        })).EnsureSuccessStatusCode();

        (await teacher.PostAsJsonAsync("/api/payments", new
        {
            parentId, lessonIds = new[] { lessonId }, paidDate = DateOnly.FromDateTime(DateTime.UtcNow),
            method = (string?)null, note = (string?)null
        })).EnsureSuccessStatusCode();
    }

    private record IdDto(Guid Id);
    private record ParentDto(Guid Id, string FullName, string Email, string? Phone);
}
