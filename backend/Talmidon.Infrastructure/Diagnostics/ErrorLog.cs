using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Talmidon.Domain.Entities;
using Talmidon.Infrastructure.Data;
using Talmidon.Infrastructure.Multitenancy;

namespace Talmidon.Infrastructure.Diagnostics;

/// <inheritdoc cref="IErrorLog"/>
public sealed class ErrorLog(
    DbContextOptions<TalmidonDbContext> options,
    ILogger<ErrorLog> logger) : IErrorLog
{
    /// <summary>
    /// אותה תקלה בדיוק, באותו נתיב, נרשמת פעם בדקה. פעמון ההתראות נמשך מהדפדפן כל כמה
    /// שניות — תקלה אחת שם הייתה כותבת אלפי שורות ביום לבסיס נתונים בגודל חצי ג'יגה,
    /// ומטביעה את שאר התקלות ברשימה. מה שנחסם עדיין מגיע ליומן הרגיל.
    /// </summary>
    private static readonly TimeSpan RepeatWindow = TimeSpan.FromMinutes(1);

    private const int MaxTrackedKeys = 500;

    private static readonly ConcurrentDictionary<string, long> LastWrites = new();

    public async Task<string> RecordAsync(Exception exception, ErrorOrigin origin)
    {
        var reference = ErrorReference.New();
        var kind = exception.GetType().Name;

        // גם כשהכתיבה למסד מצליחה — היומן של ספק האירוח הוא הדרך למצוא את התקלה
        // אם המסד עצמו הוא מה שנפל, ולכן הוא תמיד ראשון ותמיד נכתב.
        logger.LogError(exception, "Unhandled error {Reference} in {Source} {Method} {Path}",
            reference, origin.Source, origin.Method, origin.Path);

        if (!ShouldPersist($"{kind}|{origin.Path}")) return reference;

        try
        {
            // DbContext עצמאי ולא זה של הבקשה: החריגה עשויה להיות דווקא כישלון שמירה,
            // ואז ה-DbContext של הבקשה עדיין נושא את השינויים שנכשלו — שמירה עליו
            // הייתה מנסה לכתוב אותם שוב. גם דייר אין כאן: השורה אינה שייכת לאיש.
            await using var db = new TalmidonDbContext(options, new NullCurrentTenant());
            db.ErrorLog.Add(new ErrorLogEntry
            {
                Id = Guid.NewGuid(),
                Reference = reference,
                OccurredAt = DateTimeOffset.UtcNow,
                Source = origin.Source,
                Kind = kind,
                Message = ErrorDetail.Message(exception),
                Detail = ErrorDetail.Detail(exception),
                Path = Trim(origin.Path, 500),
                Method = Trim(origin.Method, 10),
                UserId = Trim(origin.UserId, 450),
                UserEmail = Trim(origin.UserEmail, 256),
                TenantId = origin.TenantId,
                IsHandled = false
            });

            // בלי אסימון ביטול במכוון: כשמשתמשת סוגרת את הלשונית באמצע בקשה שנכשלה,
            // האסימון של הבקשה כבר מבוטל — ודווקא התקלה הזו לא הייתה נרשמת.
            await db.SaveChangesAsync();
        }
        catch (Exception failure)
        {
            logger.LogError(failure, "Could not store error {Reference}.", reference);
        }

        return reference;
    }

    private static string? Trim(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        return trimmed.Length <= max ? trimmed : trimmed[..max];
    }

    private static bool ShouldPersist(string key)
    {
        var now = Stopwatch.GetTimestamp();

        if (LastWrites.TryGetValue(key, out var previous) && Stopwatch.GetElapsedTime(previous) < RepeatWindow)
            return false;

        LastWrites[key] = now;

        // מילון שגדל לנצח הוא דליפת זיכרון קטנה שמחכה; נתיב משתנה (מזהה בכתובת)
        // יוצר מפתח חדש בכל בקשה, ולכן הגיזום אינו תיאורטי.
        if (LastWrites.Count > MaxTrackedKeys)
        {
            foreach (var (staleKey, timestamp) in LastWrites)
            {
                if (Stopwatch.GetElapsedTime(timestamp) >= RepeatWindow)
                    LastWrites.TryRemove(staleKey, out _);
            }
        }

        return true;
    }
}
