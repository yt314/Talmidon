using System.Net;
using System.Net.Http.Json;

namespace Talmidon.Tests;

/// <summary>
/// הצעות תחומי הלימוד: המנהל מוסיף ומסתיר, והמורות רואות את התוצאה בהשלמה האוטומטית.
/// לא היה מכוסה בכלל. כל בדיקה משתמשת בשם ייחודי משלה, כי הרשימה משותפת לכל המערכת.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class SubjectSuggestionsTests(TalmidonWebApplicationFactory factory)
{
    private const string BuiltIn = "מתמטיקה";

    [Fact]
    public async Task AddedSuggestion_IsTrimmed_AndOfferedToTeachers()
    {
        var admin = await TestHelpers.CreateAuthorizedAdminClientAsync(factory);
        var teacher = await TestHelpers.CreateAuthorizedTeacherClientAsync(factory, "subjAddT");
        var name = UniqueName("קליגרפיה");

        Assert.Equal(HttpStatusCode.NoContent, (await AddAsync(admin, $"  {name}  ")).StatusCode);

        var row = (await AdminListAsync(admin)).Single(r => r.Name == name);
        Assert.False(row.IsHidden || row.IsBuiltIn || row.IsInUse);
        Assert.Contains(name, await TeacherListAsync(teacher));
    }

    [Fact]
    public async Task HiddenSuggestion_IsNoLongerOffered_AndAddingItAgainRestoresIt()
    {
        var admin = await TestHelpers.CreateAuthorizedAdminClientAsync(factory);
        var teacher = await TestHelpers.CreateAuthorizedTeacherClientAsync(factory, "subjHideT");
        var name = UniqueName("אסטרולוגיה");
        await AddAsync(admin, name);

        Assert.Equal(HttpStatusCode.NoContent, (await HideAsync(admin, name)).StatusCode);
        Assert.True((await AdminListAsync(admin)).Single(r => r.Name == name).IsHidden);
        Assert.DoesNotContain(name, await TeacherListAsync(teacher));

        await AddAsync(admin, name);
        Assert.Contains(name, await TeacherListAsync(teacher));
    }

    [Fact]
    public async Task HidingWorksForANameNoOneAddedYet_AndIgnoresCase()
    {
        var admin = await TestHelpers.CreateAuthorizedAdminClientAsync(factory);
        var name = UniqueName("Spam");

        await HideAsync(admin, name.ToUpperInvariant());

        Assert.True((await AdminListAsync(admin)).Single(r => string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase)).IsHidden);
        await AddAsync(admin, name);
        Assert.False((await AdminListAsync(admin)).Single(r => string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase)).IsHidden);
    }

    [Fact]
    public async Task BuiltInSubjects_AreMarked_AndCannotBeHidden()
    {
        var admin = await TestHelpers.CreateAuthorizedAdminClientAsync(factory);

        Assert.True((await AdminListAsync(admin)).Single(r => r.Name == BuiltIn).IsBuiltIn);
        Assert.Equal(HttpStatusCode.BadRequest, (await HideAsync(admin, BuiltIn)).StatusCode);
    }

    [Fact]
    public async Task BlankNames_AreRejected()
    {
        var admin = await TestHelpers.CreateAuthorizedAdminClientAsync(factory);

        Assert.Equal(HttpStatusCode.BadRequest, (await AddAsync(admin, "   ")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await HideAsync(admin, "  ")).StatusCode);
    }

    private static string UniqueName(string prefix) => $"{prefix}-{Guid.NewGuid():N}"[..(prefix.Length + 9)];

    private static Task<HttpResponseMessage> AddAsync(HttpClient admin, string name) =>
        admin.PostAsJsonAsync("/api/admin/subject-suggestions", new { name });

    private static Task<HttpResponseMessage> HideAsync(HttpClient admin, string name) =>
        admin.DeleteAsync($"/api/admin/subject-suggestions?name={Uri.EscapeDataString(name)}");

    private static async Task<List<SuggestionRow>> AdminListAsync(HttpClient admin) =>
        (await admin.GetFromJsonAsync<List<SuggestionRow>>("/api/admin/subject-suggestions"))!;

    private static async Task<List<string>> TeacherListAsync(HttpClient teacher) =>
        (await teacher.GetFromJsonAsync<List<string>>("/api/teachers/subject-suggestions"))!;

    private record SuggestionRow(string Name, bool IsHidden, bool IsBuiltIn, bool IsInUse);
}
