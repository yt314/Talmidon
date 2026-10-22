using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Talmidon.Tests;

/// <summary>
/// הספרייה הציבורית — לא הייתה מכוסה. העיקר כאן הוא פרטיות: מורה שלא בחרה להופיע
/// אינה ברשימה, ואין גישה לכרטיס או לתמונה שלה. כל בדיקה משתמשת בעיר ובתחום ייחודיים,
/// כי הספרייה משותפת לכל המורות שהבדיקות יוצרות.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class PublicLibraryTests(TalmidonWebApplicationFactory factory)
{
    [Fact]
    public async Task ATeacherWhoIsNotPublic_IsInvisible_IncludingHerCardAndPhoto()
    {
        var city = Unique("עיר");
        var (teacher, id) = await TeacherAsync("hiddenT", city, Unique("תחום"), isPublic: false);
        await UploadPhotoAsync(teacher);
        var anonymous = factory.CreateClient();

        Assert.DoesNotContain(await ListAsync(anonymous, $"city={Uri.EscapeDataString(city)}"), t => t.Id == id);
        Assert.DoesNotContain(city, (await anonymous.GetFromJsonAsync<List<string>>("/api/public/teachers/cities"))!);
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync($"/api/public/teachers/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync($"/api/public/teachers/{id}/photo")).StatusCode);
    }

    [Fact]
    public async Task APublicTeacher_IsListed_FilterableBySubjectCityAndName_AndHasACardAndPhoto()
    {
        var (city, subject) = (Unique("עיר"), Unique("תחום"));
        var (teacher, id) = await TeacherAsync("publicT", city, subject, isPublic: true);
        await UploadPhotoAsync(teacher);
        var anonymous = factory.CreateClient();

        Assert.Equal(id, Assert.Single(await ListAsync(anonymous, $"city={Uri.EscapeDataString(city)}")).Id);
        Assert.Equal(id, Assert.Single(await ListAsync(anonymous, $"subject={Uri.EscapeDataString(subject)}")).Id);
        Assert.Empty(await ListAsync(anonymous, $"city={Uri.EscapeDataString(city)}&search={Uri.EscapeDataString(Unique("שם"))}"));
        Assert.Contains(subject, (await anonymous.GetFromJsonAsync<List<string>>("/api/public/teachers/subjects"))!);
        Assert.Contains(city, (await anonymous.GetFromJsonAsync<List<string>>("/api/public/teachers/cities"))!);

        var card = await anonymous.GetFromJsonAsync<PublicTeacherDto>($"/api/public/teachers/{id}");
        Assert.Equal((city, 140m), (card!.City, card.DefaultPricePerLesson));
        var photo = await anonymous.GetAsync($"/api/public/teachers/{id}/photo");
        Assert.Equal(HttpStatusCode.OK, photo.StatusCode);
        Assert.Equal("image/png", photo.Content.Headers.ContentType!.MediaType);
    }

    // ----- עזר -----

    private static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid():N}"[..(prefix.Length + 9)];

    private static async Task<List<PublicTeacherDto>> ListAsync(HttpClient client, string query) =>
        (await client.GetFromJsonAsync<List<PublicTeacherDto>>($"/api/public/teachers?{query}"))!;

    private async Task<(HttpClient Teacher, Guid Id)> TeacherAsync(string prefix, string city, string subject, bool isPublic)
    {
        var teacher = await TestHelpers.CreateAuthorizedTeacherClientAsync(factory, prefix);
        (await teacher.PutAsJsonAsync("/api/teachers/me/subjects", new { names = new[] { subject } })).EnsureSuccessStatusCode();
        var update = await teacher.PutAsJsonAsync("/api/teachers/me", new
        {
            phone = "0500000000", contactEmail = (string?)null, city, neighborhood = (string?)null, bio = "ביוגרפיה",
            defaultPricePerLesson = 140m, pricePer45Minutes = (decimal?)null, pricePer30Minutes = (decimal?)null,
            defaultDurationMinutes = 60, rulesText = (string?)null, isPublic
        });
        update.EnsureSuccessStatusCode();
        var me = await teacher.GetFromJsonAsync<IdDto>("/api/teachers/me");
        return (teacher, me!.Id);
    }

    private static async Task UploadPhotoAsync(HttpClient teacher)
    {
        var file = new ByteArrayContent(new byte[32]);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        (await teacher.PostAsync("/api/teachers/me/photo", new MultipartFormDataContent { { file, "file", "photo" } }))
            .EnsureSuccessStatusCode();
    }

    private record IdDto(Guid Id);
    private record PublicTeacherDto(Guid Id, string FullName, string? City, decimal DefaultPricePerLesson);
}
