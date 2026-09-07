using System.ComponentModel.DataAnnotations;

namespace Talmidon.Api.Contracts;

/// <summary>
/// הודעה מהאתר. רק תוכן ההודעה נדרש — דרישת פרטי קשר הייתה מסננת בדיוק את הדיווחים
/// המהירים שבגללם הפיצ'ר קיים.
/// </summary>
public record CreateSiteFeedbackRequest(
    [Required, MaxLength(4000)] string Message,
    [MaxLength(256)] string? ContactInfo,
    [MaxLength(500)] string? PageUrl);

public record SiteFeedbackDto(
    Guid Id,
    string Message,
    string? ContactInfo,
    string? PageUrl,
    bool IsHandled,
    DateTimeOffset CreatedAt);
