using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Talmidon.Tests;

/// <summary>כותרות האבטחה שה-API מוסיף לכל תשובה — גם לשגיאות ולהפניות.</summary>
[Collection(IntegrationTestCollection.Name)]
public class SecurityHeadersTests(TalmidonWebApplicationFactory factory)
{
    private static void AssertLockedDown(HttpResponseMessage response)
    {
        Assert.Equal("nosniff", Single(response, "X-Content-Type-Options"));
        Assert.Equal("DENY", Single(response, "X-Frame-Options"));
        Assert.Equal("no-referrer", Single(response, "Referrer-Policy"));
        Assert.Equal("default-src 'none'; frame-ancestors 'none'", Single(response, "Content-Security-Policy"));
    }

    private static string Single(HttpResponseMessage response, string name) =>
        Assert.Single(response.Headers.GetValues(name));

    [Fact]
    public async Task OrdinaryResponseIsLockedDown()
    {
        var response = await factory.CreateClient().GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertLockedDown(response);
    }

    /// <summary>תמונת פרופיל היא תוכן שהמורה העלתה, ומוגשת ישירות לדפדפן.</summary>
    [Fact]
    public async Task NotFoundPhotoIsLockedDownToo()
    {
        var response = await factory.CreateClient().GetAsync($"/api/public/teachers/{Guid.NewGuid()}/photo");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        AssertLockedDown(response);
    }

    [Fact]
    public async Task UnauthorizedResponseIsLockedDownToo()
    {
        var response = await factory.CreateClient().GetAsync("/api/students");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        AssertLockedDown(response);
    }

    /// <summary>
    /// קישור אישור המייל קובע Referrer-Policy בעצמו, כי הטוקן בכתובת. הכותרת הכללית
    /// לא מוסיפה ערך שני לצידו.
    /// </summary>
    [Fact]
    public async Task EndpointThatSetsAHeaderItselfKeepsASingleValue()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/api/auth/confirm-email");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        AssertLockedDown(response);
    }

    [Fact]
    public async Task HangfireDashboardIsLeftWithoutTheClosedPolicy()
    {
        var response = await factory.CreateClient().GetAsync("/hangfire");

        Assert.False(response.Headers.Contains("Content-Security-Policy"));
        Assert.Equal("nosniff", Single(response, "X-Content-Type-Options"));
    }
}
