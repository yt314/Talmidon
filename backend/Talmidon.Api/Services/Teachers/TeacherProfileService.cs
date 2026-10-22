using Microsoft.EntityFrameworkCore;
using Talmidon.Api.Contracts;
using Talmidon.Domain;
using Talmidon.Domain.Entities;
using Talmidon.Infrastructure.Data;
using Talmidon.Infrastructure.Multitenancy;

namespace Talmidon.Api.Services.Teachers;

/// <summary>
/// פרופיל המורה המחוברת: פרטים, תחומים ותמונה (T9). Teacher ו-TeacherSubject אינם מסוננים
/// ב-Global Query Filter (הם הדייר עצמו / מידע ציבורי), לכן כל שאילתה כאן מסננת במפורש
/// לפי TenantId (= Teacher.Id).
/// </summary>
public class TeacherProfileService(TalmidonDbContext db, ICurrentTenant currentTenant)
{
    private Guid TenantId => currentTenant.TenantId
        ?? throw new InvalidOperationException("No tenant in the current context.");

    /// <summary>
    /// הטלה של עמודות בלבד: ‎PhotoData‎ הוא blob, וטעינת הישות הייתה מושכת אותו מהמסד בכל
    /// טעינת פרופיל. ‎Length‎ בתוך ההטלה מיתרגם ל-‎length()‎ של Postgres ואינו קורא את התוכן.
    /// </summary>
    public async Task<TeacherProfileDto?> GetAsync()
    {
        var row = await db.Teachers
            .Where(t => t.Id == TenantId)
            .Select(t => new
            {
                t.Id, t.FullName, t.Phone, t.ContactEmail, t.ContactInfo, t.City, t.Neighborhood, t.Bio,
                t.DefaultPricePerLesson, t.PricePer45Minutes, t.PricePer30Minutes, t.DefaultDurationMinutes,
                t.RulesText, t.IsPublic, t.AcceptingStudents,
                Subjects = t.Subjects.Select(s => new SubjectDto(s.Id, s.Name)).ToList(),
                PhotoLength = t.PhotoData == null ? (int?)null : t.PhotoData.Length
            })
            .FirstOrDefaultAsync();
        if (row is null) return null;

        var isComplete = TeacherProfileRules.IsComplete(
            row.Subjects.Count, row.DefaultPricePerLesson, row.Phone, row.ContactEmail, row.ContactInfo);
        return new TeacherProfileDto(
            row.Id, row.FullName, row.Phone, row.ContactEmail, row.City, row.Neighborhood, row.Bio,
            row.DefaultPricePerLesson, row.PricePer45Minutes, row.PricePer30Minutes,
            row.DefaultDurationMinutes, row.RulesText, row.IsPublic, row.Subjects, row.PhotoLength,
            isComplete, row.AcceptingStudents);
    }

    public async Task<ServiceError?> UpdateAsync(UpdateTeacherProfileRequest request)
    {
        var teacher = await CurrentTeacherAsync();
        if (teacher is null) return ServiceError.NotFound();

        teacher.Phone = request.Phone;
        teacher.ContactEmail = request.ContactEmail;
        teacher.City = request.City;
        teacher.Neighborhood = request.Neighborhood;
        teacher.Bio = request.Bio;
        teacher.DefaultPricePerLesson = request.DefaultPricePerLesson;
        teacher.PricePer45Minutes = request.PricePer45Minutes;
        teacher.PricePer30Minutes = request.PricePer30Minutes;
        teacher.DefaultDurationMinutes = request.DefaultDurationMinutes;
        teacher.RulesText = request.RulesText;
        teacher.IsPublic = request.IsPublic;
        if (request.AcceptingStudents is { } accepting) teacher.AcceptingStudents = accepting;
        await db.SaveChangesAsync();
        return null;
    }

    // ===== תחומים =====

    public async Task<ServiceResult<SubjectDto>> AddSubjectAsync(string name)
    {
        var trimmed = name.Trim();
        if (await db.TeacherSubjects.AnyAsync(s => s.TeacherId == TenantId && s.Name == trimmed))
            return ServiceError.Conflict("התחום כבר קיים.");

        var subject = new TeacherSubject { Id = Guid.NewGuid(), TeacherId = TenantId, Name = trimmed };
        db.TeacherSubjects.Add(subject);
        await db.SaveChangesAsync();
        return new SubjectDto(subject.Id, subject.Name);
    }

    public async Task<ServiceError?> DeleteSubjectAsync(Guid id)
    {
        var subject = await db.TeacherSubjects.FirstOrDefaultAsync(s => s.Id == id && s.TeacherId == TenantId);
        if (subject is null) return ServiceError.NotFound();

        db.TeacherSubjects.Remove(subject);
        await db.SaveChangesAsync();
        return null;
    }

    /// <summary>מחליף את כל רשימת התחומים בבת אחת. שמות ריקים וכפילויות מסוננים.</summary>
    public async Task<List<SubjectDto>> SetSubjectsAsync(IEnumerable<string?> names)
    {
        var cleaned = names
            .Select(n => n?.Trim() ?? string.Empty)
            .Where(n => n.Length is > 0 and <= 100)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var existing = await db.TeacherSubjects.Where(s => s.TeacherId == TenantId).ToListAsync();
        db.TeacherSubjects.RemoveRange(existing);

        var subjects = cleaned
            .Select(n => new TeacherSubject { Id = Guid.NewGuid(), TeacherId = TenantId, Name = n })
            .ToList();
        db.TeacherSubjects.AddRange(subjects);
        await db.SaveChangesAsync();

        return subjects.Select(s => new SubjectDto(s.Id, s.Name)).ToList();
    }

    // ===== תמונה =====

    /// <summary>גודל מרבי לתמונה. הדפדפן מקטין לריבוע לפני השליחה, ולכן זו תקרה נדיבה.</summary>
    public const int MaxPhotoBytes = 1024 * 1024;

    private static readonly string[] AllowedPhotoTypes = ["image/jpeg", "image/png", "image/webp"];

    /// <returns>חותם הגרסה החדש של התמונה (גודלה בבתים).</returns>
    public async Task<ServiceResult<int>> SetPhotoAsync(IFormFile? file)
    {
        if (file is null || file.Length == 0) return ServiceError.Invalid("לא נבחר קובץ.");
        if (file.Length > MaxPhotoBytes) return ServiceError.Invalid("הקובץ גדול מדי (עד 1MB).");
        if (!AllowedPhotoTypes.Contains(file.ContentType))
            return ServiceError.Invalid("סוג קובץ לא נתמך. יש להעלות JPG, PNG או WEBP.");

        var teacher = await CurrentTeacherAsync();
        if (teacher is null) return ServiceError.NotFound();

        using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer);
        teacher.PhotoData = buffer.ToArray();
        teacher.PhotoContentType = file.ContentType;
        await db.SaveChangesAsync();
        return teacher.PhotoData.Length;
    }

    public async Task<ServiceError?> ClearPhotoAsync()
    {
        var teacher = await CurrentTeacherAsync();
        if (teacher is null) return ServiceError.NotFound();

        teacher.PhotoData = null;
        teacher.PhotoContentType = null;
        await db.SaveChangesAsync();
        return null;
    }

    private Task<Teacher?> CurrentTeacherAsync() => db.Teachers.FirstOrDefaultAsync(t => t.Id == TenantId);
}
