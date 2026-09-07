using System.Net;
using System.Net.Http.Json;
using Talmidon.Api.Contracts;

namespace Talmidon.Tests;

/// <summary>
/// הודעות מהאתר: כל מבקר יכול לשלוח בלי להתחבר, ורק מנהל יכול לקרוא. שתי התכונות האלה
/// הן הפיצ'ר עצמו — פתוח לשליחה, סגור לקריאה.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class SiteFeedbackTests(TalmidonWebApplicationFactory factory)
{
    [Fact]
    public async Task AnonymousVisitorCanSend_AndAdminSeesIt()
    {
        var anon = factory.CreateClient();
        var marker = $"תקלה לבדיקה {Guid.NewGuid():N}";

        var response = await anon.PostAsJsonAsync("/api/public/feedback", new
        {
            message = marker,
            contactInfo = "0501234567",
            pageUrl = "https://talmidon.vercel.app/app/lessons"
        });
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var admin = await TestHelpers.CreateAuthorizedAdminClientAsync(factory);
        var rows = await admin.GetFromJsonAsync<List<SiteFeedbackDto>>("/api/admin/feedback");

        var saved = Assert.Single(rows!.Where(f => f.Message == marker));
        Assert.Equal("0501234567", saved.ContactInfo);
        Assert.Equal("https://talmidon.vercel.app/app/lessons", saved.PageUrl);
        Assert.False(saved.IsHandled);
    }

    /// <summary>הודעה בלי פרטי קשר היא מקרה תקין ולא שגיאת ולידציה.</summary>
    [Fact]
    public async Task MessageWithoutContactDetailsIsAccepted()
    {
        var anon = factory.CreateClient();

        var response = await anon.PostAsJsonAsync("/api/public/feedback", new
        {
            message = "רעיון: כפתור לשליחת תזכורת בווצאפ",
            contactInfo = (string?)null,
            pageUrl = (string?)null
        });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task EmptyMessageIsRejected()
    {
        var anon = factory.CreateClient();

        var response = await anon.PostAsJsonAsync("/api/public/feedback", new
        {
            message = "",
            contactInfo = (string?)null,
            pageUrl = (string?)null
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task TeacherCannotReadFeedback()
    {
        var teacher = await TestHelpers.CreateAuthorizedTeacherClientAsync(factory, "feedbackT");

        var response = await teacher.GetAsync("/api/admin/feedback");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task MarkingHandled_RemovesItFromTheOpenList()
    {
        var anon = factory.CreateClient();
        var marker = $"לטיפול {Guid.NewGuid():N}";
        await anon.PostAsJsonAsync("/api/public/feedback", new
        {
            message = marker,
            contactInfo = (string?)null,
            pageUrl = (string?)null
        });

        var admin = await TestHelpers.CreateAuthorizedAdminClientAsync(factory);
        var open = await admin.GetFromJsonAsync<List<SiteFeedbackDto>>("/api/admin/feedback");
        var row = Assert.Single(open!.Where(f => f.Message == marker));

        var handled = await admin.PostAsync($"/api/admin/feedback/{row.Id}/handled", null);
        Assert.Equal(HttpStatusCode.NoContent, handled.StatusCode);

        var stillOpen = await admin.GetFromJsonAsync<List<SiteFeedbackDto>>("/api/admin/feedback");
        Assert.DoesNotContain(stillOpen!, f => f.Message == marker);

        var all = await admin.GetFromJsonAsync<List<SiteFeedbackDto>>("/api/admin/feedback?includeHandled=true");
        Assert.Contains(all!, f => f.Message == marker && f.IsHandled);
    }
}
