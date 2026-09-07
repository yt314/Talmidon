using System.Net;
using System.Net.Http.Json;
using Talmidon.Api.Contracts;

namespace Talmidon.Tests;

/// <summary>
/// אירועים אישיים ביומן: ניהול מלא למורה, בידוד בין מורות, ואי-חשיפה להורה או לתלמיד.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class CalendarEventTests(TalmidonWebApplicationFactory factory)
{
    [Fact]
    public async Task Teacher_CanCreateReadUpdateAndDelete()
    {
        var teacher = await TestHelpers.CreateAuthorizedTeacherClientAsync(factory, "calEventCrud");
        var start = DateTimeOffset.UtcNow.AddDays(3);

        var created = await teacher.PostAsJsonAsync("/api/calendar-events", new
        {
            title = "פגישה בבית הספר",
            startTime = start,
            endTime = start.AddHours(1),
            isAllDay = false,
            notes = "להביא את הטפסים"
        });
        created.EnsureSuccessStatusCode();
        var dto = await created.Content.ReadFromJsonAsync<CalendarEventDto>();
        Assert.Equal("פגישה בבית הספר", dto!.Title);
        Assert.False(dto.IsAllDay);

        var updated = await teacher.PutAsJsonAsync($"/api/calendar-events/{dto.Id}", new
        {
            title = "פגישה בבית הספר — נדחתה",
            startTime = start.AddDays(1),
            endTime = start.AddDays(1).AddHours(2),
            isAllDay = false,
            notes = (string?)null
        });
        updated.EnsureSuccessStatusCode();
        var afterUpdate = await updated.Content.ReadFromJsonAsync<CalendarEventDto>();
        Assert.Equal("פגישה בבית הספר — נדחתה", afterUpdate!.Title);
        Assert.Null(afterUpdate.Notes);

        var deleted = await teacher.DeleteAsync($"/api/calendar-events/{dto.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        var remaining = await teacher.GetFromJsonAsync<List<CalendarEventDto>>("/api/calendar-events");
        Assert.DoesNotContain(remaining!, e => e.Id == dto.Id);
    }

    /// <summary>
    /// אירוע רב-יומי שהתחיל לפני תחילת החלון חייב עדיין להופיע בו — אחרת חופשה של שבוע
    /// הייתה נעלמת מהיומן ביום השני שלה.
    /// </summary>
    [Fact]
    public async Task RangeFilter_IncludesAnEventThatStartedBeforeTheWindow()
    {
        var teacher = await TestHelpers.CreateAuthorizedTeacherClientAsync(factory, "calEventRange");
        var start = new DateTimeOffset(2027, 5, 3, 0, 0, 0, TimeSpan.Zero);

        var created = await teacher.PostAsJsonAsync("/api/calendar-events", new
        {
            title = "חופשה",
            startTime = start,
            endTime = start.AddDays(7),
            isAllDay = true,
            notes = (string?)null
        });
        created.EnsureSuccessStatusCode();

        // חלון שמתחיל באמצע החופשה
        var windowFrom = Uri.EscapeDataString(start.AddDays(3).ToString("O"));
        var windowTo = Uri.EscapeDataString(start.AddDays(4).ToString("O"));
        var inWindow = await teacher.GetFromJsonAsync<List<CalendarEventDto>>(
            $"/api/calendar-events?from={windowFrom}&to={windowTo}");

        Assert.Contains(inWindow!, e => e.Title == "חופשה" && e.IsAllDay);

        // חלון אחרי סוף החופשה — לא אמור להחזיר אותה
        var afterFrom = Uri.EscapeDataString(start.AddDays(8).ToString("O"));
        var afterTo = Uri.EscapeDataString(start.AddDays(9).ToString("O"));
        var afterWindow = await teacher.GetFromJsonAsync<List<CalendarEventDto>>(
            $"/api/calendar-events?from={afterFrom}&to={afterTo}");

        Assert.DoesNotContain(afterWindow!, e => e.Title == "חופשה");
    }

    [Fact]
    public async Task EndBeforeStartIsRejected()
    {
        var teacher = await TestHelpers.CreateAuthorizedTeacherClientAsync(factory, "calEventOrder");
        var start = DateTimeOffset.UtcNow.AddDays(2);

        var response = await teacher.PostAsJsonAsync("/api/calendar-events", new
        {
            title = "אירוע הפוך",
            startTime = start,
            endTime = start.AddHours(-1),
            isAllDay = false,
            notes = (string?)null
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task OneTeacherCannotSeeOrDeleteAnothersEvent()
    {
        var first = await TestHelpers.CreateAuthorizedTeacherClientAsync(factory, "calEventOwner");
        var second = await TestHelpers.CreateAuthorizedTeacherClientAsync(factory, "calEventOther");
        var start = DateTimeOffset.UtcNow.AddDays(5);

        var created = await first.PostAsJsonAsync("/api/calendar-events", new
        {
            title = "אירוע פרטי",
            startTime = start,
            endTime = start.AddHours(1),
            isAllDay = false,
            notes = (string?)null
        });
        created.EnsureSuccessStatusCode();
        var dto = await created.Content.ReadFromJsonAsync<CalendarEventDto>();

        var otherList = await second.GetFromJsonAsync<List<CalendarEventDto>>("/api/calendar-events");
        Assert.DoesNotContain(otherList!, e => e.Id == dto!.Id);

        var otherDelete = await second.DeleteAsync($"/api/calendar-events/{dto!.Id}");
        Assert.Equal(HttpStatusCode.NotFound, otherDelete.StatusCode);

        var stillThere = await first.GetFromJsonAsync<List<CalendarEventDto>>("/api/calendar-events");
        Assert.Contains(stillThere!, e => e.Id == dto.Id);
    }
}
