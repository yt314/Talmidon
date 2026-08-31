using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Talmidon.Domain.Entities;
using Talmidon.Domain.Enums;
using Talmidon.Infrastructure.Data;
using Talmidon.Infrastructure.Email;

namespace Talmidon.Infrastructure.BackgroundJobs;

/// <summary>
/// שולח להורים תזכורת על שיעורים מתוזמנים ב-24 השעות הקרובות. רץ בתדירות שעתית (Hangfire),
/// ומסמן כל שיעור כ"נשלחה תזכורת" כדי למנוע כפילות. סורק את כל הדיירים (אין הקשר דייר בעבודת רקע).
/// </summary>
public class LessonReminderJob(
    TalmidonDbContext db, IEmailSender emailSender, ILogger<LessonReminderJob> logger)
{
    /// <summary>חלון התזכורת: שיעורים שמתחילים עד 24 שעות קדימה.</summary>
    private static readonly TimeSpan ReminderHorizon = TimeSpan.FromHours(24);

    public async Task<int> RunForAllTenantsAsync()
    {
        var now = DateTimeOffset.UtcNow;
        var horizon = now.Add(ReminderHorizon);

        var lessons = db.Lessons.IgnoreQueryFilters();
        var studentParents = db.StudentParents.IgnoreQueryFilters();

        var due = await lessons
            .Where(l => l.Status == LessonStatus.Scheduled
                && l.ReminderSentAt == null
                && l.StartTime > now
                && l.StartTime <= horizon)
            .Select(l => new { l.Id, l.StudentId, StudentName = l.Student.FullName, l.StartTime })
            .ToListAsync();

        if (due.Count == 0) return 0;

        var studentIds = due.Select(d => d.StudentId).Distinct().ToList();
        var parentLinks = await studentParents
            .Where(sp => studentIds.Contains(sp.StudentId))
            .Select(sp => new { sp.StudentId, sp.Parent })
            .ToListAsync();

        var parentsByStudent = parentLinks
            .GroupBy(x => x.StudentId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Parent).ToList());

        // ריכוז לפי הורה — מייל אחד עם כל השיעורים הקרובים
        var byParent = new Dictionary<Guid, (Parent Parent, List<(string StudentName, DateTimeOffset StartTime)> Items)>();
        foreach (var d in due)
        {
            if (!parentsByStudent.TryGetValue(d.StudentId, out var parents)) continue;
            foreach (var parent in parents)
            {
                if (!byParent.TryGetValue(parent.Id, out var entry))
                {
                    entry = (parent, new List<(string, DateTimeOffset)>());
                    byParent[parent.Id] = entry;
                }
                entry.Items.Add((d.StudentName, d.StartTime));
            }
        }

        var sent = 0;
        foreach (var (parent, items) in byParent.Values)
        {
            var lines = items
                .OrderBy(i => i.StartTime)
                .Select(i => $"<li>{WebUtility.HtmlEncode(i.StudentName)} — {i.StartTime:dd/MM/yyyy} בשעה {i.StartTime:HH:mm}</li>");
            var html = EmailTemplates.SimpleListEmail(
                "תזכורת שיעור", $"שלום {parent.FullName},", "תזכורת לשיעורים הקרובים:", lines);

            try
            {
                await emailSender.SendAsync(parent.Email, "תזכורת שיעור", html);
                sent++;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to send lesson reminder email.");
            }
        }

        // מסמנים את כל השיעורים שבחלון כ"נשלחה תזכורת" — כדי למנוע שליחה חוזרת בריצה הבאה
        var dueIds = due.Select(d => d.Id).ToList();
        await lessons
            .Where(l => dueIds.Contains(l.Id))
            .ExecuteUpdateAsync(s => s.SetProperty(l => l.ReminderSentAt, now));

        logger.LogInformation("Lesson reminders: sent {Sent} emails for {Count} lessons.", sent, due.Count);
        return sent;
    }
}
