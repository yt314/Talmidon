namespace Talmidon.Api.Security;

/// <summary>
/// כותרות אבטחה לכל תשובה של ה-API. ה-API מחזיר JSON, קובצי CSV ותמונות פרופיל, ואף
/// אחד מהם אינו דף שאמור לרוץ בדפדפן — ולכן המדיניות סוגרת הכול: אין הרצת תוכן, אין
/// הטמעה במסגרת, ואין ניחוש סוג קובץ. האחרון חשוב לתמונות: סוג התמונה נבדק מול רשימה
/// מותרת, אבל התוכן עצמו הוא מה שהמורה העלתה.
///
/// הכותרות נקבעות רק אם התשובה עוד לא קבעה אותן, כך שנקודה שצריכה ערך משלה (למשל
/// Referrer-Policy בקישור אישור המייל) שומרת עליו. הן נקבעות ברגע שליחת התשובה, ולכן
/// חלות גם על תשובות שגיאה שנבנות מחדש בהמשך הצינור.
/// </summary>
public static class SecurityHeaders
{
    private static readonly (string Name, string Value)[] Defaults =
    [
        ("X-Content-Type-Options", "nosniff"),
        ("X-Frame-Options", "DENY"),
        ("Referrer-Policy", "no-referrer"),
        ("Content-Security-Policy", "default-src 'none'; frame-ancestors 'none'")
    ];

    /// <summary>לוח הבקרה של Hangfire (בפיתוח בלבד) הוא דף HTML עם סקריפטים, ו-CSP סגור היה שובר אותו.</summary>
    private const string HangfireDashboardPath = "/hangfire";

    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            context.Response.OnStarting(() =>
            {
                var headers = context.Response.Headers;
                foreach (var (name, value) in Defaults)
                {
                    if (name == "Content-Security-Policy" && context.Request.Path.StartsWithSegments(HangfireDashboardPath))
                        continue;
                    headers.TryAdd(name, value);
                }
                return Task.CompletedTask;
            });
            await next();
        });
}
