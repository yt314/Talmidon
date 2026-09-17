using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Talmidon.Api.Contracts;
using Talmidon.Domain.Entities;
using Talmidon.Domain.Enums;
using Talmidon.Infrastructure.Data;

namespace Talmidon.Tests;

/// <summary>
/// שליפת פנייה אחת. מסך התלמידים קורא לה כשמוסיפים תלמידה מתוך פנייה, ולכן היא
/// מחזירה את פרטי הפונה — שם, טלפון ומייל. בדיוק בגלל זה היא חייבת להיות חסומה
/// לפניות של מורה אחרת.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class ContactRequestLookupTests(TalmidonWebApplicationFactory factory)
{
    [Fact]
    public async Task GetById_ReturnsTheDetailsTheParentTyped()
    {
        var teacher = await TestHelpers.CreateAuthorizedTeacherClientAsync(factory, "contactGet");
        var contactId = await SeedContactAsync(await TenantIdAsync(teacher), "רבקה מזרחי", "052-8889999", "rivka@example.com");

        var contact = await teacher.GetFromJsonAsync<ContactRequestDto>($"/api/contact-requests/{contactId}");

        Assert.Equal("רבקה מזרחי", contact!.FullName);
        Assert.Equal("052-8889999", contact.Phone);
        Assert.Equal("rivka@example.com", contact.Email);
    }

    /// <summary>
    /// פנייה מכילה שם, טלפון ומייל של אדם. מזהה מנוחש אינו אמור להחזיר אותם למורה אחרת.
    /// </summary>
    [Fact]
    public async Task GetById_AnotherTeachersContact_IsNotFound()
    {
        var owner = await TestHelpers.CreateAuthorizedTeacherClientAsync(factory, "contactOwner");
        var stranger = await TestHelpers.CreateAuthorizedTeacherClientAsync(factory, "contactStranger");
        var contactId = await SeedContactAsync(await TenantIdAsync(owner), "רבקה מזרחי", "052-8889999", "rivka@example.com");

        var response = await stranger.GetAsync($"/api/contact-requests/{contactId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>פנייה שנמחקה בינתיים. המסך מתאושש מזה ופותח טופס ריק, ולכן 404 ולא שגיאה.</summary>
    [Fact]
    public async Task GetById_UnknownId_IsNotFound()
    {
        var teacher = await TestHelpers.CreateAuthorizedTeacherClientAsync(factory, "contactMissing");

        var response = await teacher.GetAsync($"/api/contact-requests/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ----- עזר -----

    private static async Task<Guid> TenantIdAsync(HttpClient teacher) =>
        (await teacher.GetFromJsonAsync<TeacherProfileDto>("/api/teachers/me"))!.Id;

    /// <summary>
    /// כותבת פנייה ישירות, כמו שהנתיב הציבורי עושה: אין דייר בהקשר של פנייה אנונימית,
    /// ולכן ה-TenantId מפורש.
    /// </summary>
    private async Task<Guid> SeedContactAsync(Guid tenantId, string fullName, string phone, string email)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TalmidonDbContext>();

        var contact = new ContactRequest
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FullName = fullName,
            Phone = phone,
            Email = email,
            Subject = "מתמטיקה לכיתה ח׳",
            Message = "האם יש מקום פנוי בשעות אחר הצהריים?",
            Status = ContactRequestStatus.New,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.ContactRequests.Add(contact);
        await db.SaveChangesAsync();
        return contact.Id;
    }
}
