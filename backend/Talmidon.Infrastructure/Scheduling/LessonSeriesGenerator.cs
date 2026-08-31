using Talmidon.Domain.Common;
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

            // בונים את המופע מהזמן המקומי (תאריך + שעת-היום הקבועה של הסדרה) ורק אז ממירים ל-UTC —
            // כך ה-offset הנכון (קיץ/חורף) נבחר מחדש לכל תאריך בנפרד, ושעת-היום המקומית לא זזה.
            var startTime = AppTimeZone.ToUtc(nextDate, series.StartTimeOfDay);
            db.Lessons.Add(new Lesson
            {
                Id = Guid.NewGuid(),
                TenantId = series.TenantId,
                StudentId = series.StudentId,
                StartTime = startTime,
                EndTime = startTime.AddMinutes(series.DurationMinutes),
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
