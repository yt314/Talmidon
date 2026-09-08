using System.ComponentModel.DataAnnotations;

namespace Talmidon.Api.Contracts;

/// <summary>
/// בקשה לבניית מערך שיעור. האורכים מוגבלים כי כל בקשה עולה כסף, ומורה שמדביקה ספר שלם
/// לתוך "הערות" הייתה מייקרת אותה בלי להשתפר.
/// </summary>
public record BuildLessonPlanRequest(
    [Required, MaxLength(100)] string Subject,
    [Required, MaxLength(300)] string Topic,
    [Range(15, 240)] int DurationMinutes,
    [MaxLength(100)] string? GradeLevel,
    [MaxLength(1000)] string? Notes);

public record LessonPlanResponse(string Plan);

/// <summary>
/// האם תכונות ה-AI זמינות בשרת, ומי הספק הפעיל. הממשק מסתיר את מה שאינו מוגדר,
/// ומציג את שם הספק — כך רואים במסך עצמו אם רץ המודל החינמי או זה שבתשלום.
/// </summary>
public record AiAvailabilityDto(bool LessonPlanner, string Provider);
