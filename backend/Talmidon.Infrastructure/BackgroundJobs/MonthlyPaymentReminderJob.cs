using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Talmidon.Domain.Entities;
using Talmidon.Infrastructure.Data;
using Talmidon.Infrastructure.Email;

namespace Talmidon.Infrastructure.BackgroundJobs;

/// <summary>
/// שולח תזכורת תשלום לכל הורה עם חיובים פתוחים (מרוכזת לפי ילד). משמש גם את התזמון
/// האוטומטי החודשי (Hangfire, כל הדיירים) וגם את הכפתור הידני של המורה (דייר נוכחי בלבד).
/// </summary>
public class MonthlyPaymentReminderJob(
    TalmidonDbContext db, IEmailSender emailSender, ILogger<MonthlyPaymentReminderJob> logger)
{
    /// <summary>נקרא ע"י הריצה החודשית האוטומטית — אין הקשר דייר (HTTP) בעבודת רקע, לכן מתעלמים מה-Global Query Filter וסורקים את כל הדיירים.</summary>
    public Task<int> RunForAllTenantsAsync() => RunAsync(ignoreTenantFilter: true);

    /// <summary>נקרא ע"י כפתור "שלח עכשיו" של מורה מחוברת — מכבד את ה-Global Query Filter ומוגבל לדייר הנוכחי בלבד.</summary>
    public Task<int> RunForCurrentTenantAsync() => RunAsync(ignoreTenantFilter: false);

    private async Task<int> RunAsync(bool ignoreTenantFilter)
    {
        var lessons = ignoreTenantFilter ? db.Lessons.IgnoreQueryFilters() : db.Lessons;
        var studentParents = ignoreTenantFilter ? db.StudentParents.IgnoreQueryFilters() : db.StudentParents;

        var openCharges = await (
            from l in lessons
            where l.PaymentRequired && l.PaymentId == null
            join sp in studentParents on l.StudentId equals sp.StudentId
            select new { sp.Parent, l.Student.FullName, l.StartTime, l.Amount })
            .ToListAsync();

        var sentCount = 0;
        foreach (var group in openCharges.GroupBy(x => x.Parent))
        {
            var charges = group.Select(g => (g.FullName, g.StartTime, g.Amount)).ToList();
            if (await SendReminderAsync(group.Key, charges))
                sentCount++;
        }

        logger.LogInformation(
            "Monthly payment reminders: sent {SentCount} of {TotalParents} (all tenants: {AllTenants}).",
            sentCount, openCharges.Select(x => x.Parent.Id).Distinct().Count(), ignoreTenantFilter);

        return sentCount;
    }

    private async Task<bool> SendReminderAsync(
        Parent parent, List<(string StudentName, DateTimeOffset StartTime, decimal Amount)> charges)
    {
        var total = charges.Sum(c => c.Amount);
        var lines = charges.Select(c =>
            $"<li>{WebUtility.HtmlEncode(c.StudentName)} — {c.StartTime:dd/MM/yyyy} — ₪{c.Amount}</li>");
        var html = EmailTemplates.SimpleListEmail(
            "תזכורת תשלום חודשית",
            $"שלום {parent.FullName},",
            $"להלן החיובים הפתוחים לתשלום, בסך כולל של ₪{total}:",
            lines);

        try
        {
            await emailSender.SendAsync(parent.Email, "תזכורת תשלום חודשית", html);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send monthly payment reminder email.");
            return false;
        }
    }
}
