using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Talmidon.Api.Contracts;
using Talmidon.Api.Services;
using Talmidon.Domain.Common;
using Talmidon.Domain.Entities;
using Talmidon.Domain.Enums;
using Talmidon.Infrastructure.Auth;
using Talmidon.Infrastructure.Data;
using Talmidon.Infrastructure.Email;
using Talmidon.Infrastructure.Multitenancy;
using Talmidon.Infrastructure.Scheduling;

namespace Talmidon.Api.Controllers;

/// <summary>
/// סדרות שיעורים חוזרות שבועיות (T4). כל סדרה מייצרת שורות Lesson רגילות ועצמאיות לגמרי —
/// שינוי/ביטול/השלמה פועלים על מופע בודד בלבד, לא על הסדרה (ראו LessonsController).
/// </summary>
[ApiController]
[Route("api/lesson-series")]
[Authorize(Roles = Roles.Teacher)]
public class LessonSeriesController(
    TalmidonDbContext db,
    ICurrentTenant currentTenant,
    LessonSeriesGenerator generator,
    FamilyNotifier familyNotifier) : ControllerBase
{
    private const int HorizonWeeks = 8;

    private Guid TenantId => currentTenant.TenantId
        ?? throw new InvalidOperationException("No tenant in the current context.");

    /// <summary>יוצרת סדרה ומייצרת מיד את המופעים הראשונים (עד לאופק הייצור), כדי שהמורה תראה שיעורים בלוח מיידית.</summary>
    [HttpPost]
    public async Task<ActionResult<LessonSeriesDto>> Create(CreateLessonSeriesRequest request)
    {
        if (request.FirstEndTime <= request.FirstStartTime)
            return BadRequest(new { message = "שעת הסיום חייבת להיות אחרי שעת ההתחלה." });

        var validationError = ValidateEndCondition(request);
        if (validationError is not null)
            return BadRequest(new { message = validationError });

        var student = await db.Students.FirstOrDefaultAsync(s => s.Id == request.StudentId);
        if (student is null) return NotFound(new { message = "תלמיד לא נמצא." });

        // יום-בשבוע ושעת-היום של הסדרה נגזרים מהזמן המקומי (לא מ-UTC הגולמי) — אחרת "16:00" שנבחרה
        // בפועל בישראל הייתה נשמרת כ"14:00" (ה-offset של UTC), ומחליקה שעה קדימה/אחורה סביב שעון קיץ/חורף.
        var localFirstStart = AppTimeZone.ToLocal(request.FirstStartTime);

        var series = new LessonSeries
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            StudentId = student.Id,
            DayOfWeek = localFirstStart.DayOfWeek,
            StartTimeOfDay = TimeOnly.FromDateTime(localFirstStart.DateTime),
            DurationMinutes = (int)(request.FirstEndTime - request.FirstStartTime).TotalMinutes,
            SeriesStartDate = DateOnly.FromDateTime(localFirstStart.DateTime),
            EndDate = request.EndCondition == LessonSeriesEndCondition.EndDate ? request.EndDate : null,
            OccurrenceCount = request.EndCondition == LessonSeriesEndCondition.Count ? request.OccurrenceCount : null,
            IsActive = true,
            SkipJewishHolidays = request.SkipJewishHolidays ?? true,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.LessonSeries.Add(series);

        var horizon = AppTimeZone.Today.AddDays(7 * HorizonWeeks);
        var created = await generator.GenerateOccurrencesAsync(series, horizon);

        return Ok(new LessonSeriesDto(
            series.Id, series.StudentId, student.FullName, series.DayOfWeek, series.StartTimeOfDay,
            series.DurationMinutes, series.EndDate, series.OccurrenceCount, series.OccurrencesGenerated,
            series.IsActive, created));
    }

    /// <summary>
    /// מבטלת סדרה (מפסיקה ייצור עתידי). <paramref name="deleteFutureOccurrences"/> קובע אם גם למחוק
    /// שיעורים עתידיים שכבר נוצרו ועדיין מתוזמנים — לעולם לא נוגעת בשיעורים שהתקיימו/בוטלו/עברו.
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<CancelLessonSeriesResultDto>> Cancel(Guid id, [FromQuery] bool deleteFutureOccurrences = false)
    {
        var series = await db.LessonSeries.Include(s => s.Student).FirstOrDefaultAsync(s => s.Id == id);
        if (series is null) return NotFound();

        series.IsActive = false;

        var cancelled = new List<DateTimeOffset>();
        if (deleteFutureOccurrences)
        {
            var now = DateTimeOffset.UtcNow;
            var futureOccurrences = await db.Lessons
                .Where(l => l.SeriesId == id && l.Status == LessonStatus.Scheduled && l.StartTime > now)
                .ToListAsync();
            cancelled.AddRange(futureOccurrences.Select(l => l.StartTime).Order());
            db.Lessons.RemoveRange(futureOccurrences);
        }

        await db.SaveChangesAsync();

        // רק כשנמחקו שיעורים בפועל. הפסקת ייצור עתידי לבדה אינה משנה דבר ביומן
        // של המשפחה — אין על מה להודיע.
        if (cancelled.Count > 0)
        {
            var studentName = series.Student.FullName;
            await familyNotifier.NotifyFamilyAsync(series.StudentId,
                EmailSubjects.LessonSeriesCancelled(studentName, cancelled.Count),
                $"השיעור הקבוע של {studentName} לא יימשך, והשיעורים הבאים שכבר נקבעו בוטלו:",
                CancelledLessonLines(cancelled));
        }

        return Ok(new CancelLessonSeriesResultDto(cancelled.Count));
    }

    /// <summary>
    /// התאריכים שבוטלו, לגוף המייל. רשימה ארוכה במייל אינה נקראת, ולכן מעבר
    /// לתקרה נאמר רק כמה נשארו.
    /// </summary>
    private const int MaxListedDates = 10;

    private static List<string> CancelledLessonLines(IReadOnlyList<DateTimeOffset> cancelled)
    {
        // המרה לשעון ישראל: ב-DB נשמר UTC, וקורא המייל חושב בשעון שלו.
        var lines = cancelled
            .Take(MaxListedDates)
            .Select(AppTimeZone.ToLocal)
            .Select(d => $"{d:dd/MM/yyyy} בשעה {d:HH:mm}")
            .ToList();

        if (cancelled.Count > MaxListedDates)
            lines.Add($"ועוד {cancelled.Count - MaxListedDates} שיעורים.");

        return lines;
    }

    private static string? ValidateEndCondition(CreateLessonSeriesRequest request)
    {
        switch (request.EndCondition)
        {
            case LessonSeriesEndCondition.Count when request.OccurrenceCount is null:
                return "יש לציין מספר שיעורים.";
            case LessonSeriesEndCondition.EndDate when request.EndDate is null:
                return "יש לציין תאריך סיום.";
            case LessonSeriesEndCondition.Indefinite when request.OccurrenceCount is not null || request.EndDate is not null:
                return "סדרה ללא הגבלה לא יכולה לכלול מספר שיעורים או תאריך סיום.";
        }
        if (request.EndCondition != LessonSeriesEndCondition.Count && request.OccurrenceCount is not null)
            return "מספר שיעורים רלוונטי רק במצב \"מספר שיעורים קבוע\".";
        if (request.EndCondition != LessonSeriesEndCondition.EndDate && request.EndDate is not null)
            return "תאריך סיום רלוונטי רק במצב \"עד תאריך\".";
        return null;
    }
}
