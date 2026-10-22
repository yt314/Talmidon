using Microsoft.EntityFrameworkCore;
using Talmidon.Api.Contracts;
using Talmidon.Domain.Entities;
using Talmidon.Infrastructure.Data;
using Talmidon.Infrastructure.Multitenancy;

namespace Talmidon.Api.Services.Teachers;

/// <summary>שעות הזמינות השבועיות של המורה — להדגשה ביומן ולאזהרה בקביעה מחוץ לשעות.</summary>
public class TeacherAvailabilityService(TalmidonDbContext db, ICurrentTenant currentTenant)
{
    private Guid TenantId => currentTenant.TenantId
        ?? throw new InvalidOperationException("No tenant in the current context.");

    public Task<List<AvailabilityWindowDto>> GetAsync() =>
        db.TeacherAvailabilities
            .OrderBy(a => a.DayOfWeek).ThenBy(a => a.StartTime)
            .Select(a => new AvailabilityWindowDto((int)a.DayOfWeek, a.StartTime.ToString("HH:mm"), a.EndTime.ToString("HH:mm")))
            .ToListAsync();

    /// <summary>
    /// מחליף את כל החלונות בבת אחת (עריכת הלוח השבועי ושמירה). חלון לא תקין אחד דוחה את
    /// הכל — והלוח הקודם נשאר כמו שהוא.
    /// </summary>
    public async Task<ServiceError?> ReplaceAsync(IEnumerable<AvailabilityWindowDto> windows)
    {
        var parsed = new List<TeacherAvailability>();
        foreach (var window in windows)
        {
            var (entity, error) = Parse(window);
            if (error is not null) return error;
            parsed.Add(entity!);
        }

        var existing = await db.TeacherAvailabilities.ToListAsync();
        db.TeacherAvailabilities.RemoveRange(existing);
        db.TeacherAvailabilities.AddRange(parsed);
        await db.SaveChangesAsync();
        return null;
    }

    private (TeacherAvailability? Entity, ServiceError? Error) Parse(AvailabilityWindowDto window)
    {
        if (window.DayOfWeek is < 0 or > 6)
            return (null, ServiceError.Invalid("יום לא תקין."));
        if (!TimeOnly.TryParse(window.StartTime, out var start) || !TimeOnly.TryParse(window.EndTime, out var end))
            return (null, ServiceError.Invalid("שעה לא תקינה."));
        if (end <= start)
            return (null, ServiceError.Invalid("שעת הסיום חייבת להיות אחרי שעת ההתחלה."));

        return (new TeacherAvailability
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            DayOfWeek = (DayOfWeek)window.DayOfWeek,
            StartTime = start,
            EndTime = end
        }, null);
    }
}
