namespace Talmidon.Infrastructure.Diagnostics;

/// <summary>מה שידוע על ההקשר שבו נפלה התקלה. הכול אופציונלי חוץ מהמקור.</summary>
public sealed record ErrorOrigin
{
    /// <summary><c>request</c> או <c>job</c>.</summary>
    public required string Source { get; init; }

    public string? Path { get; init; }
    public string? Method { get; init; }
    public string? UserId { get; init; }
    public string? UserEmail { get; init; }
    public Guid? TenantId { get; init; }
}

/// <summary>
/// רושם תקלה ומחזיר את הקוד הקצר שלה. לעולם אינו זורק: תקלה ברישום תקלה לא
/// אמורה להפיל את הבקשה שכבר נכשלה ממילא.
/// </summary>
public interface IErrorLog
{
    Task<string> RecordAsync(Exception exception, ErrorOrigin origin);
}
