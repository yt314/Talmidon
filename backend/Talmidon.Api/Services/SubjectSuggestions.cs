using Microsoft.EntityFrameworkCore;
using Talmidon.Api.Contracts;
using Talmidon.Domain;
using Talmidon.Domain.Entities;
using Talmidon.Infrastructure.Data;

namespace Talmidon.Api.Services;

/// <summary>
/// הצעות תחומי הלימוד להשלמה אוטומטית: הקטלוג הקבוע, תחומים שמורות כבר הזינו, ומה
/// שהמנהל הוסיף — פחות מה שהמנהל הסתיר. אינה רשימה סגורה: מורה יכולה להקליד כל תחום.
/// </summary>
public class SubjectSuggestions(TalmidonDbContext db)
{
    private static readonly HashSet<string> BuiltIn = SubjectCatalog.Common.ToHashSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>מה שמוצע למורה. מה שהמנהל הסתיר יורד גם אם מורה כלשהי כבר הזינה אותו.</summary>
    public async Task<List<string>> ForTeachersAsync()
    {
        var (inUse, curated) = await LoadAsync();
        var hidden = HiddenNames(curated);

        return AllNames(inUse, curated.Where(x => !x.IsHidden))
            .Where(n => !hidden.Contains(n))
            .ToList();
    }

    /// <summary>
    /// הרשימה כפי שמורה רואה אותה, עם סימון מה מוסתר, מה מהקטלוג הקבוע (לא ניתן להסרה
    /// מכאן — הוא חלק מהקוד) ומה כבר בשימוש.
    /// </summary>
    public async Task<List<AdminSubjectSuggestionDto>> ForAdminAsync()
    {
        var (inUse, curated) = await LoadAsync();
        var hidden = HiddenNames(curated);
        var used = inUse.ToHashSet(StringComparer.OrdinalIgnoreCase);

        return AllNames(inUse, curated)
            .Select(name => new AdminSubjectSuggestionDto(name, hidden.Contains(name), BuiltIn.Contains(name), used.Contains(name)))
            .ToList();
    }

    /// <summary>
    /// מוסיף או מסתיר שם. הוספה מחדש של שם שהוסתר מבטלת את ההסתרה. הסתרה אינה נוגעת
    /// בפרופילים: מורה שכבר בחרה בתחום שומרת אותו, והוא פשוט לא מוצע יותר לאחרות.
    /// </summary>
    public async Task<ServiceError?> SetHiddenAsync(string? name, bool hidden)
    {
        var trimmed = (name ?? string.Empty).Trim();
        if (trimmed.Length == 0) return ServiceError.Invalid("שם ריק.");
        if (hidden && BuiltIn.Contains(trimmed))
            return ServiceError.Invalid("תחום מהקטלוג הקבוע אינו ניתן להסתרה.");

        var existing = await db.SubjectSuggestions.FirstOrDefaultAsync(x => x.Name.ToLower() == trimmed.ToLower());
        if (existing is null)
        {
            db.SubjectSuggestions.Add(new SubjectSuggestion
            {
                Id = Guid.NewGuid(),
                Name = trimmed,
                IsHidden = hidden,
                CreatedAt = DateTimeOffset.UtcNow
            });
        }
        else
        {
            existing.IsHidden = hidden;
        }

        await db.SaveChangesAsync();
        return null;
    }

    private async Task<(List<string> InUse, List<SubjectSuggestion> Curated)> LoadAsync()
    {
        var inUse = await db.TeacherSubjects.Select(s => s.Name).Distinct().ToListAsync();
        var curated = await db.SubjectSuggestions.ToListAsync();
        return (inUse, curated);
    }

    private static HashSet<string> HiddenNames(IEnumerable<SubjectSuggestion> curated) =>
        curated.Where(x => x.IsHidden).Select(x => x.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static IEnumerable<string> AllNames(IEnumerable<string> inUse, IEnumerable<SubjectSuggestion> curated) =>
        SubjectCatalog.Common
            .Concat(inUse)
            .Concat(curated.Select(x => x.Name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(n => n, StringComparer.CurrentCulture);
}
