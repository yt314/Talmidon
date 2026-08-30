using Talmidon.Domain.Entities;
using Talmidon.Domain.Enums;
using Talmidon.Infrastructure.Data;

namespace Talmidon.Infrastructure.Scheduling;

/// <summary>
/// מייצר מתוך <see cref="LessonSeries"/> שורות <see cref="Lesson"/> רגילות ועצמאיות, עד לאופק
/// נתון (חלון מתגלגל — לא כל העתיד בבת אחת). אידמפוטנטי: קריאה חוזרת לאותו אופק לא מייצרת
/// כפילויות, כי <see cref="LessonSeries.LastGeneratedDate"/> משמש כסימניית המשך. נקרא גם
/// באופן מיידי בעת יצירת סדרה (כדי שהמורה תראה שיעורים בלוח מיד), וגם מתוך עבודת הרקע היומית
/// שמגלגלת קדימה סדרות פעילות (ראו <c>LessonSeriesGenerationJob</c>).
/// </summary>
public class LessonSeriesGenerator(TalmidonDbContext db)
{
    public async Task<int> GenerateOccurrencesAsync(LessonSeries series, DateOnly horizon, CancellationToken ct = default)
    {
        if (!series.IsActive) return 0;

        var effectiveHorizon = series.EndDate is { } endDate && endDate < horizon ? endDate : horizon;
        var nextDate = series.LastGeneratedDate?.AddDays(7) ?? series.SeriesStartDate;
        var created = 0;

        while (nextDate <= effectiveHorizon)
        {
            if (series.OccurrenceCount is { } count && series.OccurrencesGenerated >= count)
                break;

            var startDateTime = nextDate.ToDateTime(series.StartTimeOfDay, DateTimeKind.Utc);
            db.Lessons.Add(new Lesson
            {
                Id = Guid.NewGuid(),
                TenantId = series.TenantId,
                StudentId = series.StudentId,
                StartTime = new DateTimeOffset(startDateTime, TimeSpan.Zero),
                EndTime = new DateTimeOffset(startDateTime.AddMinutes(series.DurationMinutes), TimeSpan.Zero),
                Status = LessonStatus.Scheduled,
                Origin = LessonOrigin.Teacher,
                SeriesId = series.Id
            });

            series.OccurrencesGenerated++;
            series.LastGeneratedDate = nextDate;
            created++;
            nextDate = nextDate.AddDays(7);
        }

        if (series.OccurrenceCount is { } finalCount && series.OccurrencesGenerated >= finalCount)
            series.IsActive = false;
        else if (series.EndDate is { } finalEndDate && series.LastGeneratedDate >= finalEndDate)
            series.IsActive = false;

        if (created > 0 || db.ChangeTracker.HasChanges())
            await db.SaveChangesAsync(ct);

        return created;
    }
}
