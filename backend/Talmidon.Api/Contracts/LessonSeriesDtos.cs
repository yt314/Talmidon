using System.ComponentModel.DataAnnotations;
using Talmidon.Domain.Enums;

namespace Talmidon.Api.Contracts;

/// <summary>
/// יוצרת סדרת שיעורים חוזרת שבועית. יום-בשבוע/שעה/משך נגזרים אוטומטית מ-FirstStartTime/FirstEndTime —
/// אין צורך לבחור אותם בנפרד. בדיוק אחד מ-OccurrenceCount/EndDate צריך להיות מוגדר, בהתאם ל-EndCondition.
/// </summary>
public record CreateLessonSeriesRequest(
    [Required] Guid StudentId,
    [Required] DateTimeOffset FirstStartTime,
    [Required] DateTimeOffset FirstEndTime,
    [Required] LessonSeriesEndCondition EndCondition,
    [Range(1, 500)] int? OccurrenceCount,
    DateOnly? EndDate);

public record LessonSeriesDto(
    Guid Id,
    Guid StudentId,
    string StudentName,
    DayOfWeek DayOfWeek,
    TimeOnly StartTimeOfDay,
    int DurationMinutes,
    DateOnly? EndDate,
    int? OccurrenceCount,
    int OccurrencesGenerated,
    bool IsActive,
    int OccurrencesCreated);
