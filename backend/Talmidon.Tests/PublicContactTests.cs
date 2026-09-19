using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Talmidon.Api.Contracts;
using Talmidon.Infrastructure.Data;

namespace Talmidon.Tests;

/// <summary>
/// The enquiry form on a public teacher card. The profile screen tells the
/// teacher that switching "accepting new students" off means parents do not
/// contact her for nothing, so the public endpoint has to hold that line — the
/// form being hidden is not enough, since a page opened before she switched it
/// off would still post.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class PublicContactTests(TalmidonWebApplicationFactory factory)
{
    [Fact]
    public async Task Contact_WhenSheIsAcceptingStudents_IsDelivered()
    {
        var teacherId = await PublicTeacherAsync("contactOpen", acceptingStudents: true);
        var anon = factory.CreateClient();

        var response = await anon.PostAsJsonAsync($"/api/public/teachers/{teacherId}/contact", Enquiry("רבקה מזרחי"));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(1, await EnquiryCountAsync(teacherId));
    }

    [Fact]
    public async Task Contact_WhenSheIsNotAcceptingStudents_IsRefusedAndNothingIsStored()
    {
        var teacherId = await PublicTeacherAsync("contactClosed", acceptingStudents: false);
        var anon = factory.CreateClient();

        var response = await anon.PostAsJsonAsync($"/api/public/teachers/{teacherId}/contact", Enquiry("רבקה מזרחי"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(0, await EnquiryCountAsync(teacherId));
    }

    // ----- helpers -----

    private static CreateContactRequestRequest Enquiry(string fullName) =>
        new(fullName, "052-8889999", "rivka@example.com", "מתמטיקה לכיתה ח׳", "האם יש מקום פנוי אחר הצהריים?");

    /// <summary>Registers a teacher and puts her card in the public library with the given availability.</summary>
    private async Task<Guid> PublicTeacherAsync(string emailPrefix, bool acceptingStudents)
    {
        var client = await TestHelpers.CreateAuthorizedTeacherClientAsync(factory, emailPrefix);
        var id = (await client.GetFromJsonAsync<TeacherProfileDto>("/api/teachers/me"))!.Id;

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TalmidonDbContext>();
        var teacher = await db.Teachers.IgnoreQueryFilters().SingleAsync(t => t.Id == id);
        teacher.IsPublic = true;
        teacher.AcceptingStudents = acceptingStudents;
        await db.SaveChangesAsync();
        return id;
    }

    private async Task<int> EnquiryCountAsync(Guid teacherId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TalmidonDbContext>();
        return await db.ContactRequests.IgnoreQueryFilters().CountAsync(c => c.TenantId == teacherId);
    }
}
