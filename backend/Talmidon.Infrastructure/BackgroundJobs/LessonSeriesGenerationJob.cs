using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Talmidon.Infrastructure.Data;
using Talmidon.Infrastructure.Scheduling;

namespace Talmidon.Infrastructure.BackgroundJobs;

/// <summary>
/// מגלגלת קדימה את חלון הייצור של כל סדרות השיעורים החוזרות הפעילות, לכל הדיירים —
/// כדי ששיעורים חדשים ימשיכו להופיע בלוח גם בלי שהמורה תפתח את האפליקציה. רצה פעם ביום
/// (Hangfire). אין הקשר דייר (HTTP) בעבודת רקע, לכן מתעלמים מה-Global Query Filter וסורקים
/// את כל הדיירים בבת אחת — כמו ב-MonthlyPaymentReminderJob.
/// </summary>
public class LessonSeriesGenerationJob(
    TalmidonDbContext db, LessonSeriesGenerator generator, ILogger<LessonSeriesGenerationJob> logger)
{
    private const int HorizonWeeks = 8;

    public async Task<int> RunForAllTenantsAsync()
    {
        var horizon = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(7 * HorizonWeeks);

        var activeSeries = await db.LessonSeries
            .IgnoreQueryFilters()
            .Where(s => s.IsActive)
            .ToListAsync();

        var totalCreated = 0;
        foreach (var series in activeSeries)
            totalCreated += await generator.GenerateOccurrencesAsync(series, horizon);

        logger.LogInformation(
            "Lesson series generation: created {TotalCreated} occurrences across {SeriesCount} active series.",
            totalCreated, activeSeries.Count);

        return totalCreated;
    }
}
