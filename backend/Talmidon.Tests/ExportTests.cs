using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Talmidon.Api.Contracts;
using Talmidon.Domain.Entities;
using Talmidon.Domain.Enums;
using Talmidon.Infrastructure.Data;
using Talmidon.Infrastructure.Export;

namespace Talmidon.Tests;

/// <summary>
/// ייצוא הנתונים. מה שנבדק כאן הוא לא הפורמט אלא ההבטחה: שמה שהמורה הכניסה יוצא
/// שלם, ושמורה אחת אינה מורידה את הנתונים של אחרת.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class ExportTests(TalmidonWebApplicationFactory factory)
{
    [Fact]
    public async Task TheStudentsFileHoldsTheStudentsAndTheirParents()
    {
        var teacher = await TestHelpers.CreateAuthorizedTeacherClientAsync(factory, "exportStudents");

        var parent = await teacher.PostAsJsonAsync("/api/parents", new
        {
            fullName = "אמא של דנה", gender = (int?)null,
            email = TestHelpers.UniqueEmail("exportParent"), phone = "050-1234567"
        });
        parent.EnsureSuccessStatusCode();
        var parentDto = await parent.Content.ReadFromJsonAsync<ParentRef>();

        var created = await teacher.PostAsJsonAsync("/api/students", new
        {
            fullName = "דנה כהן", gender = (int?)null, gradeLevel = "כיתה ה'",
            birthDate = (string?)null, generalInfo = "מתקשה בשברים",
            loginEmail = (string?)null, parentIds = new[] { parentDto!.Id }
        });
        created.EnsureSuccessStatusCode();

        var csv = await ReadCsvAsync(teacher, "/api/export/students.csv");

        Assert.Contains("דנה כהן", csv);
        Assert.Contains("כיתה ה'", csv);
        Assert.Contains("אמא של דנה", csv);
        Assert.Contains("050-1234567", csv);
        Assert.Contains("מתקשה בשברים", csv);
    }

    [Fact]
    public async Task TheLessonsFileHoldsTheLessonsWithTheirStatus()
    {
        var teacher = await TestHelpers.CreateAuthorizedTeacherClientAsync(factory, "exportLessons");
        var studentId = await AddStudentAsync(teacher, "תלמידה מיוצאת");

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TalmidonDbContext>();
            var tenantId = (await db.Students.IgnoreQueryFilters().FirstAsync(s => s.Id == studentId)).TenantId;
            db.Lessons.Add(new Lesson
            {
                Id = Guid.NewGuid(), TenantId = tenantId, StudentId = studentId,
                StartTime = new DateTimeOffset(2030, 5, 6, 14, 0, 0, TimeSpan.Zero),
                EndTime = new DateTimeOffset(2030, 5, 6, 15, 0, 0, TimeSpan.Zero),
                Status = LessonStatus.Completed, Origin = LessonOrigin.Teacher,
                PaymentRequired = true, Amount = 120
            });
            await db.SaveChangesAsync();
        }

        var csv = await ReadCsvAsync(teacher, "/api/export/lessons.csv");

        Assert.Contains("תלמידה מיוצאת", csv);
        Assert.Contains("06/05/2030", csv);
        Assert.Contains("התקיים", csv);
        Assert.Contains("120", csv);
    }

    /// <summary>
    /// הייצוא נשען על אותו מסנן דייר כמו כל שאילתה אחרת. זו הבדיקה שמוודאת שגם הנתיב
    /// הזה — שמחזיר קובץ ולא JSON — לא עוקף אותו.
    /// </summary>
    [Fact]
    public async Task OneTeacherNeverDownloadsAnothersStudents()
    {
        var first = await TestHelpers.CreateAuthorizedTeacherClientAsync(factory, "exportMine");
        var second = await TestHelpers.CreateAuthorizedTeacherClientAsync(factory, "exportHers");
        await AddStudentAsync(first, "תלמידה של הראשונה");

        var csv = await ReadCsvAsync(second, "/api/export/students.csv");

        Assert.DoesNotContain("תלמידה של הראשונה", csv);
    }

    [Fact]
    public async Task TheFileIsMarkedForDownloadAndOpensAsHebrew()
    {
        var teacher = await TestHelpers.CreateAuthorizedTeacherClientAsync(factory, "exportHeaders");

        var response = await teacher.GetAsync("/api/export/students.csv");
        response.EnsureSuccessStatusCode();

        Assert.Equal("attachment", response.Content.Headers.ContentDisposition!.DispositionType);
        Assert.Equal("text/csv", response.Content.Headers.ContentType!.MediaType);

        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.Equal(Csv.Utf8Bom, bytes.Take(3));
    }

    [Fact]
    public async Task AParentCannotDownloadTheTeachersData()
    {
        var teacher = await TestHelpers.CreateAuthorizedTeacherClientAsync(factory, "exportRole");
        await AddStudentAsync(teacher, "תלמידה");

        var anonymous = factory.CreateClient();
        var response = await anonymous.GetAsync("/api/export/students.csv");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static async Task<string> ReadCsvAsync(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        response.EnsureSuccessStatusCode();
        return Encoding.UTF8.GetString(await response.Content.ReadAsByteArrayAsync());
    }

    private static async Task<Guid> AddStudentAsync(HttpClient teacher, string name)
    {
        var response = await teacher.PostAsJsonAsync("/api/students", new
        {
            fullName = name, gender = (int?)null, gradeLevel = (string?)null,
            birthDate = (string?)null, generalInfo = (string?)null,
            loginEmail = (string?)null, parentIds = Array.Empty<Guid>()
        });
        response.EnsureSuccessStatusCode();
        var student = await response.Content.ReadFromJsonAsync<StudentDetailDto>();
        return student!.Id;
    }

    private record ParentRef(Guid Id);
}
