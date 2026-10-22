using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Talmidon.Tests;

/// <summary>
/// הפרופיל של המורה עצמה (TeachersController) — מה שלא היה מכוסה: מתי הפרופיל נחשב
/// "מלא", עריכת התחומים, שעות הזמינות ובדיקות התקינות שלהן, ותמונת הפרופיל.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class TeacherProfileTests(TalmidonWebApplicationFactory factory)
{
    [Fact]
    public async Task Profile_BecomesComplete_WithSubjectsPriceAndAWayToMakeContact()
    {
        var teacher = await TestHelpers.CreateAuthorizedTeacherClientAsync(factory, "profComplete");
        Assert.False((await ProfileAsync(teacher)).IsProfileComplete);

        await UpdateProfileAsync(teacher, price: 150, phone: "0501234567");
        Assert.False((await ProfileAsync(teacher)).IsProfileComplete);

        (await teacher.PutAsJsonAsync("/api/teachers/me/subjects", new { names = new[] { "מתמטיקה" } })).EnsureSuccessStatusCode();
        var profile = await ProfileAsync(teacher);

        Assert.True(profile.IsProfileComplete);
        Assert.Equal((150m, "0501234567"), (profile.DefaultPricePerLesson, profile.Phone));
    }

    [Fact]
    public async Task SetSubjects_TrimsDropsBlanksAndDuplicates_AndReplacesTheWholeList()
    {
        var teacher = await TestHelpers.CreateAuthorizedTeacherClientAsync(factory, "profSubjects");
        await teacher.PutAsJsonAsync("/api/teachers/me/subjects", new { names = new[] { "פיזיקה" } });

        var response = await teacher.PutAsJsonAsync("/api/teachers/me/subjects",
            new { names = new[] { " אנגלית ", "", "אנגלית", "ENGLISH", "english", "כימיה" } });
        response.EnsureSuccessStatusCode();

        var names = (await ProfileAsync(teacher)).Subjects.Select(s => s.Name).Order().ToList();
        Assert.Equal(new[] { "ENGLISH", "אנגלית", "כימיה" }.Order(), names);
    }

    [Fact]
    public async Task AddSubject_RejectsADuplicate_AndDeleteRemovesIt()
    {
        var teacher = await TestHelpers.CreateAuthorizedTeacherClientAsync(factory, "profAddSubject");

        var added = await (await teacher.PostAsJsonAsync("/api/teachers/me/subjects", new { name = " היסטוריה " }))
            .Content.ReadFromJsonAsync<SubjectDto>();
        Assert.Equal("היסטוריה", added!.Name);
        Assert.Equal(HttpStatusCode.Conflict, (await teacher.PostAsJsonAsync("/api/teachers/me/subjects", new { name = "היסטוריה" })).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await teacher.DeleteAsync($"/api/teachers/me/subjects/{added.Id}")).StatusCode);
        Assert.Empty((await ProfileAsync(teacher)).Subjects);
        Assert.Equal(HttpStatusCode.NotFound, (await teacher.DeleteAsync($"/api/teachers/me/subjects/{added.Id}")).StatusCode);
    }

    [Fact]
    public async Task Availability_IsReplacedAndReturnedInWeekOrder()
    {
        var teacher = await TestHelpers.CreateAuthorizedTeacherClientAsync(factory, "profAvail");

        var put = await teacher.PutAsJsonAsync("/api/teachers/me/availability", new
        {
            windows = new[]
            {
                new { dayOfWeek = 3, startTime = "16:00", endTime = "19:00" },
                new { dayOfWeek = 0, startTime = "9:00", endTime = "12:30" }
            }
        });
        Assert.Equal(HttpStatusCode.NoContent, put.StatusCode);

        var windows = await teacher.GetFromJsonAsync<List<WindowDto>>("/api/teachers/me/availability");
        Assert.Equal(new[] { new WindowDto(0, "09:00", "12:30"), new WindowDto(3, "16:00", "19:00") }, windows);
    }

    [Theory]
    [InlineData(7, "09:00", "10:00")]
    [InlineData(1, "nine", "10:00")]
    [InlineData(1, "10:00", "10:00")]
    public async Task Availability_WithAnInvalidWindow_IsRejected_AndTheOldScheduleKept(int day, string start, string end)
    {
        var teacher = await TestHelpers.CreateAuthorizedTeacherClientAsync(factory, "profAvailBad");
        await teacher.PutAsJsonAsync("/api/teachers/me/availability",
            new { windows = new[] { new { dayOfWeek = 2, startTime = "08:00", endTime = "09:00" } } });

        var put = await teacher.PutAsJsonAsync("/api/teachers/me/availability", new
        {
            windows = new[]
            {
                new { dayOfWeek = 4, startTime = "08:00", endTime = "09:00" },
                new { dayOfWeek = day, startTime = start, endTime = end }
            }
        });

        Assert.Equal(HttpStatusCode.BadRequest, put.StatusCode);
        var windows = await teacher.GetFromJsonAsync<List<WindowDto>>("/api/teachers/me/availability");
        Assert.Equal(2, Assert.Single(windows!).DayOfWeek);
    }

    [Fact]
    public async Task Photo_AcceptsAnImage_RejectsOtherTypes_AndCanBeRemoved()
    {
        var teacher = await TestHelpers.CreateAuthorizedTeacherClientAsync(factory, "profPhoto");

        Assert.Equal(HttpStatusCode.BadRequest, (await UploadAsync(teacher, [1, 2, 3], "application/pdf")).StatusCode);
        Assert.Null((await ProfileAsync(teacher)).PhotoVersion);

        Assert.Equal(HttpStatusCode.OK, (await UploadAsync(teacher, new byte[64], "image/png")).StatusCode);
        Assert.Equal(64, (await ProfileAsync(teacher)).PhotoVersion);

        Assert.Equal(HttpStatusCode.NoContent, (await teacher.DeleteAsync("/api/teachers/me/photo")).StatusCode);
        Assert.Null((await ProfileAsync(teacher)).PhotoVersion);
    }

    // ----- עזר -----

    private static async Task<ProfileDto> ProfileAsync(HttpClient teacher) =>
        (await teacher.GetFromJsonAsync<ProfileDto>("/api/teachers/me"))!;

    private static async Task UpdateProfileAsync(HttpClient teacher, decimal price, string? phone)
    {
        var response = await teacher.PutAsJsonAsync("/api/teachers/me", new
        {
            phone,
            contactEmail = (string?)null,
            city = (string?)null,
            neighborhood = (string?)null,
            bio = (string?)null,
            defaultPricePerLesson = price,
            pricePer45Minutes = (decimal?)null,
            pricePer30Minutes = (decimal?)null,
            defaultDurationMinutes = 60,
            rulesText = (string?)null,
            isPublic = false
        });
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    private static Task<HttpResponseMessage> UploadAsync(HttpClient teacher, byte[] bytes, string contentType)
    {
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        var form = new MultipartFormDataContent { { file, "file", "photo" } };
        return teacher.PostAsync("/api/teachers/me/photo", form);
    }

    private record SubjectDto(Guid Id, string Name);
    private record WindowDto(int DayOfWeek, string StartTime, string EndTime);
    private record ProfileDto(
        string? Phone, decimal DefaultPricePerLesson, List<SubjectDto> Subjects, int? PhotoVersion, bool IsProfileComplete);
}
