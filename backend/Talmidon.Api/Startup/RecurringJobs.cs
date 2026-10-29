using Hangfire;
using Talmidon.Infrastructure.BackgroundJobs;

namespace Talmidon.Api.Startup;

public static class RecurringJobs
{
    /// <summary>
    /// המשימות רשומות בכל סביבה: השיעורים החוזרים והתזכורות הם חלק מהמוצר, ובלעדיהן
    /// המורה צריכה ליצור כל שיעור ביד.
    /// לוח הבקרה של Hangfire נשאר לפיתוח בלבד; הוא מאמת דרך Cookie ולא מכיר את ה-JWT.
    /// </summary>
    public static void ScheduleRecurringJobs(this WebApplication app)
    {
        // ה-API הסטטי RecurringJob שלמטה קורא מ-JobStorage.Current, ובלעדיו העלייה נופלת
        // ב-"Current JobStorage instance has not been initialized yet". השרת עצמו כבר רשום
        // ב-DI דרך AddHangfireServer ורץ כשירות רקע, ולכן נותר רק להצביע על אותו אחסון —
        // במקום UseHangfireServer, שהוא מיושן ומרים שרת שני.
        JobStorage.Current = app.Services.GetRequiredService<JobStorage>();

        RecurringJob.AddOrUpdate<MonthlyPaymentReminderJob>(
            "monthly-payment-reminders",
            job => job.RunForAllTenantsAsync(),
            Cron.Monthly(),
            new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });

        RecurringJob.AddOrUpdate<LessonSeriesGenerationJob>(
            "lesson-series-generation",
            job => job.RunForAllTenantsAsync(),
            Cron.Daily(),
            new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });

        RecurringJob.AddOrUpdate<ErrorLogCleanupJob>(
            "error-log-cleanup",
            job => job.RunAsync(),
            Cron.Daily(),
            new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });

        RecurringJob.AddOrUpdate<LessonReminderJob>(
            "lesson-reminders",
            job => job.RunForAllTenantsAsync(),
            Cron.Hourly(),
            new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });
    }
}
