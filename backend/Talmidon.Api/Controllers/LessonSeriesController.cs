using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Talmidon.Api.Contracts;
using Talmidon.Domain.Entities;
using Talmidon.Domain.Enums;
using Talmidon.Infrastructure.Auth;
using Talmidon.Infrastructure.Data;
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
    LessonSeriesGenerator generator) : ControllerBase
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

        var series = new LessonSeries
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            StudentId = student.Id,
            DayOfWeek = request.FirstStartTime.DayOfWeek,
            StartTimeOfDay = TimeOnly.FromDateTime(request.FirstStartTime.UtcDateTime),
            DurationMinutes = (int)(request.FirstEndTime - request.FirstStartTime).TotalMinutes,
            SeriesStartDate = DateOnly.FromDateTime(request.FirstStartTime.UtcDateTime),
            EndDate = request.EndCondition == LessonSeriesEndCondition.EndDate ? request.EndDate : null,
            OccurrenceCount = request.EndCondition == LessonSeriesEndCondition.Count ? request.OccurrenceCount : null,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.LessonSeries.Add(series);

        var horizon = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(7 * HorizonWeeks);
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
    public async Task<IActionResult> Cancel(Guid id, [FromQuery] bool deleteFutureOccurrences = false)
    {
        var series = await db.LessonSeries.FirstOrDefaultAsync(s => s.Id == id);
        if (series is null) return NotFound();

        series.IsActive = false;

        if (deleteFutureOccurrences)
        {
            var now = DateTimeOffset.UtcNow;
            var futureOccurrences = await db.Lessons
                .Where(l => l.SeriesId == id && l.Status == LessonStatus.Scheduled && l.StartTime > now)
                .ToListAsync();
            db.Lessons.RemoveRange(futureOccurrences);
        }

        await db.SaveChangesAsync();
        return NoContent();
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
