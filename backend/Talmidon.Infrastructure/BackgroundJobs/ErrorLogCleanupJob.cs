using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Talmidon.Infrastructure.Data;

namespace Talmidon.Infrastructure.BackgroundJobs;

/// <summary>
/// מוחק תקלות ישנות מהיומן.
///
/// בלי זה היומן הוא טבלה שרק גדלה, על בסיס נתונים שיש לו חצי ג'יגה — ותקלה חוזרת
/// אחת יכולה למלא אותו. חודש אחורה מספיק בהרבה למה שהיומן נועד לו: לאבחן משהו שקרה.
/// </summary>
public class ErrorLogCleanupJob(TalmidonDbContext db, ILogger<ErrorLogCleanupJob> logger)
{
    public static readonly TimeSpan Retention = TimeSpan.FromDays(30);

    public async Task<int> RunAsync()
    {
        var cutoff = DateTimeOffset.UtcNow - Retention;
        var removed = await db.ErrorLog.Where(e => e.OccurredAt < cutoff).ExecuteDeleteAsync();

        if (removed > 0)
            logger.LogInformation("Error log cleanup: removed {Count} entries older than {Days} days.",
                removed, Retention.TotalDays);

        return removed;
    }
}
