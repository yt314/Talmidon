using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Talmidon.Api.Contracts;
using Talmidon.Domain.Common;

namespace Talmidon.Tests;

/// <summary>
/// השעה שכתובה במייל היא השעה שהמשפחה מגיעה בה.
///
/// ב-DB נשמר UTC; המסכים ממירים לשעון ישראל, המייל לא המיר — כך ששיעור ב-17:00
/// יצא כ"בשעה 14:00". הבדיקה רצה על המסלול המלא (בקר → תבנית → דואר) ולא רק על
/// פונקציית העיצוב, כי שם בדיוק אפשר לשכוח את ההמרה שוב.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class EmailTimeZoneTests(TalmidonWebApplicationFactory factory)
{
    /// <summary>14:00Z באוקטובר = 17:00 בישראל (שעון קיץ, UTC+3).</summary>
    private static readonly DateTimeOffset FivePmIsrael = new(2026, 10, 1, 14, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ANewLessonIsAnnouncedAtTheTimeTheFamilyWillArrive()
    {
        var (teacher, studentId, parentEmail) = await CreateFamilyAsync("tzAdded");

        var response = await teacher.PostAsJsonAsync("/api/lessons", new
        {
            studentId,
            startTime = FivePmIsrael,
            endTime = FivePmIsrael.AddHours(1)
        });
        response.EnsureSuccessStatusCode();

        var mail = Assert.Single(factory.SentEmails.To(parentEmail), e => e.Subject.StartsWith("נקבע שיעור"));
        Assert.Contains("בשעה 17:00", mail.Subject);
        Assert.DoesNotContain("14:00", mail.Subject);
        // גם גוף המייל, שנבנה בנפרד משורת הנושא
        Assert.Contains("01/10/2026 17:00", mail.HtmlBody);
    }

    /// <summary>
    /// שיעור אחרי חצות בשעון ישראל נופל על היום הקודם ב-UTC. בלי המרה גם התאריך
    /// שגוי, לא רק השעה.
    /// </summary>
    [Fact]
    public async Task ALessonJustAfterMidnightKeepsTheIsraeliDate()
    {
        var (teacher, studentId, parentEmail) = await CreateFamilyAsync("tzMidnight");
        var justAfterMidnight = new DateTimeOffset(2026, 10, 1, 21, 30, 0, TimeSpan.Zero);

        var response = await teacher.PostAsJsonAsync("/api/lessons", new
        {
            studentId,
            startTime = justAfterMidnight,
            endTime = justAfterMidnight.AddHours(1)
        });
        response.EnsureSuccessStatusCode();

        var mail = Assert.Single(factory.SentEmails.To(parentEmail), e => e.Subject.StartsWith("נקבע שיעור"));
        Assert.Contains("02/10/2026", mail.Subject);
        Assert.Contains("בשעה 00:30", mail.Subject);
    }

    /// <summary>ביטול שיעור עובר במסלול אחר בבקר, ולכן נבדק בנפרד.</summary>
    [Fact]
    public async Task CancellingALessonUsesTheSameClock()
    {
        var (teacher, studentId, parentEmail) = await CreateFamilyAsync("tzCancel");
        var created = await teacher.PostAsJsonAsync("/api/lessons", new
        {
            studentId,
            startTime = FivePmIsrael,
            endTime = FivePmIsrael.AddHours(1)
        });
        created.EnsureSuccessStatusCode();
        var lesson = await created.Content.ReadFromJsonAsync<LessonDto>();

        (await teacher.DeleteAsync($"/api/lessons/{lesson!.Id}")).EnsureSuccessStatusCode();

        var mail = Assert.Single(factory.SentEmails.To(parentEmail), e => e.Subject.Contains("בוטל"));
        Assert.Contains(AppTimeZone.ToLocal(FivePmIsrael).ToString("dd/MM/yyyy"), mail.Subject);
        Assert.Contains("01/10/2026 17:00", mail.HtmlBody);
    }

    // ----- עזר -----

    private async Task<(HttpClient Teacher, Guid StudentId, string ParentEmail)> CreateFamilyAsync(string prefix)
    {
        var teacher = await TestHelpers.CreateAuthorizedTeacherClientAsync(factory, $"{prefix}T");

        var parentEmail = TestHelpers.UniqueEmail($"{prefix}P");
        var parentResponse = await teacher.PostAsJsonAsync("/api/parents", new
        {
            fullName = "הורה בדיקה",
            gender = (int?)null,
            email = parentEmail,
            phone = (string?)null
        });
        parentResponse.EnsureSuccessStatusCode();
        var parent = await parentResponse.Content.ReadFromJsonAsync<ParentIdDto>();

        var studentResponse = await teacher.PostAsJsonAsync("/api/students", new
        {
            fullName = "תלמידה בבדיקה",
            gender = (int?)null,
            gradeLevel = (string?)null,
            birthDate = (string?)null,
            generalInfo = (string?)null,
            loginEmail = (string?)null,
            parentIds = new[] { parent!.Id }
        });
        studentResponse.EnsureSuccessStatusCode();
        var student = await studentResponse.Content.ReadFromJsonAsync<StudentDetailDto>();

        return (teacher, student!.Id, parentEmail);
    }

    private record ParentIdDto(Guid Id);
}
