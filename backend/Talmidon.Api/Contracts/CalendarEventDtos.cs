using System.ComponentModel.DataAnnotations;

namespace Talmidon.Api.Contracts;

/// <summary>
/// אירוע ביומן שאינו שיעור. באירוע של יום שלם, <c>EndTime</c> הוא חצות של היום שאחרי
/// האחרון — מוסכמת "סוף בלעדי" של לוח השנה בצד הלקוח.
/// </summary>
public record CreateCalendarEventRequest(
    [Required, MaxLength(200)] string Title,
    [Required] DateTimeOffset StartTime,
    [Required] DateTimeOffset EndTime,
    bool IsAllDay,
    [MaxLength(2000)] string? Notes);

public record UpdateCalendarEventRequest(
    [Required, MaxLength(200)] string Title,
    [Required] DateTimeOffset StartTime,
    [Required] DateTimeOffset EndTime,
    bool IsAllDay,
    [MaxLength(2000)] string? Notes);

public record CalendarEventDto(
    Guid Id,
    string Title,
    DateTimeOffset StartTime,
    DateTimeOffset EndTime,
    bool IsAllDay,
    string? Notes);
