using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Talmidon.Api.Contracts;
using Talmidon.Infrastructure.Data;

namespace Talmidon.Tests;

/// <summary>
/// יומן התקלות מקצה לקצה: חריגה אמיתית עוברת את הצינור האמיתי, חוזרת ללקוח עם קוד,
/// ונמצאת אחר כך במסך הניהול לפי אותו קוד. זה כל הפיצ'ר — הקישור בין מה שהמשתמשת
/// רואה לבין עקבת המחסנית.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class ErrorLogTests(TalmidonWebApplicationFactory factory)
{
    private record ErrorResponse(string Message, string Reference);

    [Fact]
    public async Task AFailedRequestComesBackWithACodeThatLeadsToTheStackTrace()
    {
        var marker = Guid.NewGuid().ToString("N");
        var path = $"/api/test-only/throw/{marker}";

        var response = await factory.CreateClient().GetAsync(path);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.False(string.IsNullOrWhiteSpace(body!.Reference));
        Assert.False(string.IsNullOrWhiteSpace(body.Message));

        var admin = await TestHelpers.CreateAuthorizedAdminClientAsync(factory);
        var rows = await admin.GetFromJsonAsync<List<AdminErrorDto>>(
            $"/api/admin/errors?reference={body.Reference}");

        var row = Assert.Single(rows!);
        Assert.Equal("request", row.Source);
        Assert.Equal("InvalidOperationException", row.Kind);
        Assert.Contains(marker, row.Message);
        Assert.Equal(path, row.Path);
        Assert.Equal("GET", row.Method);
        Assert.False(row.IsHandled);

        var detail = await admin.GetFromJsonAsync<AdminErrorDetailDto>($"/api/admin/errors/{row.Id}");
        Assert.Contains("ThrowingTestController", detail!.Detail);
    }

    /// <summary>בלי לדעת אצל מי זה קרה, רוב התקלות לא ניתנות לשחזור.</summary>
    [Fact]
    public async Task TheSignedInUserIsRecordedWithTheError()
    {
        var email = TestHelpers.UniqueEmail("errorlog-teacher");
        const string password = "TestPass123";
        var anon = factory.CreateClient();
        await TestHelpers.RegisterAndConfirmTeacherAsync(factory, anon, email, password, "מורה בדיקה");
        var token = await TestHelpers.LoginAsync(anon, email, password);
        var teacher = TestHelpers.AuthorizedClient(factory, token);

        var response = await teacher.GetAsync($"/api/test-only/throw/{Guid.NewGuid():N}");
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();

        var admin = await TestHelpers.CreateAuthorizedAdminClientAsync(factory);
        var rows = await admin.GetFromJsonAsync<List<AdminErrorDto>>(
            $"/api/admin/errors?reference={body!.Reference}");

        var row = Assert.Single(rows!);
        Assert.Equal(email, row.UserEmail);

        var detail = await admin.GetFromJsonAsync<AdminErrorDetailDto>($"/api/admin/errors/{row.Id}");
        Assert.NotNull(detail!.TenantId);
    }

    /// <summary>
    /// הודעות שגיאה של ספקים חיצוניים מצטטות את מה שנשלח אליהם. מסך תקלות הוא לא
    /// מקום לאחסן בו מפתח פעיל, גם כשרק מנהל רואה אותו.
    /// </summary>
    [Fact]
    public async Task SecretsInsideAnErrorAreNotStored()
    {
        var marker = Guid.NewGuid().ToString("N");

        var response = await factory.CreateClient().GetAsync($"/api/test-only/throw-with-secret/{marker}");
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();

        var admin = await TestHelpers.CreateAuthorizedAdminClientAsync(factory);
        var rows = await admin.GetFromJsonAsync<List<AdminErrorDto>>(
            $"/api/admin/errors?reference={body!.Reference}");
        var row = Assert.Single(rows!);
        var detail = await admin.GetFromJsonAsync<AdminErrorDetailDto>($"/api/admin/errors/{row.Id}");

        Assert.DoesNotContain("hunter2", detail!.Message);
        Assert.DoesNotContain("sk-live-abcdef123456", detail.Message);
        Assert.DoesNotContain("hunter2", detail.Detail);
        Assert.DoesNotContain("sk-live-abcdef123456", detail.Detail);
        Assert.Contains(marker, detail.Message);
    }

    /// <summary>
    /// פעמון ההתראות נמשך מהדפדפן כל כמה שניות. תקלה אחת שם, בלי חסימת חזרות,
    /// הייתה כותבת אלפי שורות ביום וקוברת תחתיהן כל תקלה אחרת.
    /// </summary>
    [Fact]
    public async Task TheSameFailureRepeatedIsRecordedOnce()
    {
        var marker = Guid.NewGuid().ToString("N");
        var path = $"/api/test-only/throw/{marker}";
        var client = factory.CreateClient();

        for (var i = 0; i < 5; i++)
        {
            var response = await client.GetAsync(path);
            // גם מה שלא נרשם עדיין מחזיר קוד למשתמשת — החסימה היא על הכתיבה, לא על התשובה.
            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TalmidonDbContext>();
        Assert.Equal(1, await db.ErrorLog.CountAsync(e => e.Path == path));
    }

    [Fact]
    public async Task MarkingAnErrorHandledTakesItOutOfTheOpenList()
    {
        var response = await factory.CreateClient().GetAsync($"/api/test-only/throw/{Guid.NewGuid():N}");
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();

        var admin = await TestHelpers.CreateAuthorizedAdminClientAsync(factory);
        var row = Assert.Single(await admin.GetFromJsonAsync<List<AdminErrorDto>>(
            $"/api/admin/errors?reference={body!.Reference}")!);

        var marked = await admin.PostAsync($"/api/admin/errors/{row.Id}/handled?handled=true", null);
        Assert.Equal(HttpStatusCode.NoContent, marked.StatusCode);

        var open = await admin.GetFromJsonAsync<List<AdminErrorDto>>("/api/admin/errors");
        Assert.DoesNotContain(open!, e => e.Id == row.Id);

        // חיפוש לפי קוד מוצא גם מה שכבר טופל — הקוד מגיע מהמשתמשת, לא מהרשימה.
        var found = Assert.Single(await admin.GetFromJsonAsync<List<AdminErrorDto>>(
            $"/api/admin/errors?reference={body.Reference}")!);
        Assert.True(found.IsHandled);
    }

    /// <summary>היומן מכיל עקבות מחסנית של כל הדיירים — מורה אינה אמורה לראות אותו.</summary>
    [Fact]
    public async Task ATeacherCannotReadTheErrorLog()
    {
        var teacher = await TestHelpers.CreateAuthorizedTeacherClientAsync(factory, "errorlog-outsider");

        var response = await teacher.GetAsync("/api/admin/errors");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
