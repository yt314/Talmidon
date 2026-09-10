using System.Net;
using System.Net.Http.Json;
using Talmidon.Api.Contracts;

namespace Talmidon.Tests;

/// <summary>
/// מחירון לפי אורך שיעור: המורה מגדירה מחיר לשעה, ואופציונלית גם ל-45 ול-30 דקות.
/// השדות האופציונליים הם null כשלא הוגדרו — הלקוח נופל אז לחישוב יחסי מהמחיר לשעה
/// (ראו lesson-pricing.util.ts), ולכן חשוב ש-null יישמר כ-null ולא יהפוך ל-0.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class TeacherPricingTests(TalmidonWebApplicationFactory factory)
{
    [Fact]
    public async Task UpdateProfile_PersistsPerDurationPrices()
    {
        var teacher = await TestHelpers.CreateAuthorizedTeacherClientAsync(factory, "pricingSave");

        var response = await teacher.PutAsJsonAsync("/api/teachers/me", ProfileRequest(140m, 120m, 90m));
        response.EnsureSuccessStatusCode();

        var profile = await teacher.GetFromJsonAsync<TeacherProfileDto>("/api/teachers/me");
        Assert.Equal(140m, profile!.DefaultPricePerLesson);
        Assert.Equal(120m, profile.PricePer45Minutes);
        Assert.Equal(90m, profile.PricePer30Minutes);
    }

    [Fact]
    public async Task UpdateProfile_KeepsUndefinedPerDurationPricesNull()
    {
        var teacher = await TestHelpers.CreateAuthorizedTeacherClientAsync(factory, "pricingNull");

        var response = await teacher.PutAsJsonAsync("/api/teachers/me", ProfileRequest(140m, null, null));
        response.EnsureSuccessStatusCode();

        var profile = await teacher.GetFromJsonAsync<TeacherProfileDto>("/api/teachers/me");
        Assert.Null(profile!.PricePer45Minutes);
        Assert.Null(profile.PricePer30Minutes);
    }

    [Fact]
    public async Task UpdateProfile_RejectsNegativePerDurationPrice()
    {
        var teacher = await TestHelpers.CreateAuthorizedTeacherClientAsync(factory, "pricingNegative");

        var response = await teacher.PutAsJsonAsync("/api/teachers/me", ProfileRequest(140m, -1m, null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static object ProfileRequest(decimal pricePerHour, decimal? price45, decimal? price30) => new
    {
        phone = "050-1234567",
        contactEmail = (string?)null,
        city = "ירושלים",
        neighborhood = (string?)null,
        bio = (string?)null,
        defaultPricePerLesson = pricePerHour,
        pricePer45Minutes = price45,
        pricePer30Minutes = price30,
        defaultDurationMinutes = 60,
        rulesText = (string?)null,
        isPublic = true
    };
}
