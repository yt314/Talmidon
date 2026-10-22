using Microsoft.EntityFrameworkCore;
using Talmidon.Api.Contracts;
using Talmidon.Domain.Entities;
using Talmidon.Infrastructure.Data;

namespace Talmidon.Api.Services.Public;

/// <summary>
/// ספריית המורות הציבורית (P1/P2) — ללא התחברות. כל שאילתה כאן מתחילה ממורות עם
/// IsPublic=true בלבד: מורה שלא בחרה להופיע אינה ברשימה, ואין גישה לכרטיס או לתמונה שלה.
/// </summary>
public class PublicLibrary(TalmidonDbContext db)
{
    private IQueryable<Teacher> PublicTeachers => db.Teachers.Where(t => t.IsPublic);

    public Task<List<PublicTeacherSummaryDto>> ListAsync(string? subject, string? city, string? search)
    {
        var query = PublicTeachers;
        if (!string.IsNullOrWhiteSpace(subject)) query = query.Where(t => t.Subjects.Any(s => s.Name == subject));
        if (!string.IsNullOrWhiteSpace(city)) query = query.Where(t => t.City == city);
        if (!string.IsNullOrWhiteSpace(search)) query = query.Where(t => t.FullName.Contains(search));

        return query
            .OrderBy(t => t.FullName)
            .Select(t => new PublicTeacherSummaryDto(
                t.Id, t.FullName, t.Bio, t.City, t.Neighborhood, t.DefaultPricePerLesson,
                t.Subjects.Select(s => s.Name).ToList(),
                // רק אורך, לא ה-blob: אחרת כל טעינה של הספרייה הייתה מושכת את כל
                // התמונות בתוך ה-JSON. הלקוח בונה מזה את הכתובת.
                t.PhotoData == null ? (int?)null : t.PhotoData.Length,
                t.AcceptingStudents))
            .ToListAsync();
    }

    /// <summary>התחומים הקיימים בקרב מורות ציבוריות — לתפריט הסינון (P1).</summary>
    public Task<List<string>> SubjectsAsync() =>
        PublicTeachers.SelectMany(t => t.Subjects.Select(s => s.Name)).Distinct().OrderBy(s => s).ToListAsync();

    /// <summary>הערים שיש בהן מורות ציבוריות — לתפריט הסינון. ריקות אינן נספרות.</summary>
    public Task<List<string>> CitiesAsync() =>
        PublicTeachers
            .Where(t => t.City != null && t.City != "")
            .Select(t => t.City!)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync();

    public Task<PublicTeacherDetailDto?> GetAsync(Guid id) =>
        PublicTeachers
            .Where(t => t.Id == id)
            .Select(t => new PublicTeacherDetailDto(
                t.Id, t.FullName, t.Bio, t.City, t.Neighborhood, t.Phone, t.ContactEmail,
                t.DefaultPricePerLesson, t.RulesText,
                t.Subjects.Select(s => s.Name).ToList(),
                t.PhotoData == null ? (int?)null : t.PhotoData.Length,
                t.AcceptingStudents))
            .FirstOrDefaultAsync();

    /// <summary>תמונת הפרופיל — חלק מהכרטיס הציבורי, ולכן רק של מורה ציבורית.</summary>
    public async Task<(byte[] Data, string ContentType)?> PhotoAsync(Guid id)
    {
        var photo = await PublicTeachers
            .Where(t => t.Id == id && t.PhotoData != null)
            .Select(t => new { t.PhotoData, t.PhotoContentType })
            .FirstOrDefaultAsync();
        return photo is null ? null : (photo.PhotoData!, photo.PhotoContentType ?? "image/jpeg");
    }
}
