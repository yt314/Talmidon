using Hangfire;
using Microsoft.AspNetCore.HttpOverrides;
using Talmidon.Api.Diagnostics;
using Talmidon.Api.Security;
using Talmidon.Api.Startup;
using Talmidon.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

// שכבת התשתית: DbContext (PostgreSQL), Identity, טוקנים, מיילים
builder.Services.AddInfrastructure(builder.Configuration);

// שירותי ה-API, אימות JWT והגבלת קצב לנקודות האימות
builder.Services.AddApiServices();
builder.Services.AddJwtAuthentication(builder.Configuration);
builder.Services.AddAuthRateLimiting(builder.Configuration);

// CORS לאפליקציית ה-Angular
const string CorsPolicy = "TalmidonClient";
// כתובת הלקוח בייצור. ברירת המחדל אינה מסתמכת על משתנה הסביבה: כשהוא חסר או
// שגוי, שגיאת CORS מפילה כל בקשה מהדפדפן ונראית כמו שרת מת, ולכן הכתובת
// הידועה נשארת מותרת בכל מקרה.
const string ProductionOrigin = "https://talmidon.vercel.app";

// התאמה מדויקת בלבד. בדיקת סיומת על שם המארח נראית מהודקת ואינה כזו:
// "talmidon.vercel.app" הוא גם סופו של "eviltalmidon.vercel.app", ופרויקט בשם
// כזה פתוח לכל אחד להקים ב-Vercel — הדפדפן היה מתיר לדף שלו לקרוא ל-API הזה
// עם ה-token של המשתמשת. תצוגה מקדימה שצריכה גישה תתווסף לרשימה במפורש.
// הכתובת החיה מותרת תמיד, בלי תלות בסביבה. כשהיא ישבה בענף הפרודקשן בלבד,
// מופע שעלה בטעות כ-Development חסם את האתר האמיתי וחשף זאת כשגיאת CORS.
var configuredOrigins = new List<string?> { ProductionOrigin, Environment.GetEnvironmentVariable("APP_CLIENT_URL") };
if (builder.Environment.IsDevelopment())
{
    configuredOrigins.Add("http://localhost:4200");
}

var allowedOrigins = configuredOrigins
    .Where(origin => !string.IsNullOrWhiteSpace(origin))
    // כותרת Origin מגיעה תמיד בלי לוכסן מסייג, וערך עם לוכסן בסוף לא היה תואם
    .Select(origin => origin!.Trim().TrimEnd('/'))
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToArray();

builder.Services.AddCors(options => options.AddPolicy(CorsPolicy, policy =>
{
    policy.WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod();
}));

var app = builder.Build();

// מודפס פעם אחת בעלייה כדי שאפשר יהיה לראות ביומן איזו רשימה באמת נטענה,
// במקום להסיק אותה משגיאת CORS בדפדפן.
app.Logger.LogInformation("CORS allowed origins: {Origins}", string.Join(", ", allowedOrigins));

await app.InitializeDatabaseAsync();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
    // הדשבורד של Hangfire נחשף רק בפיתוח: הוא מבוסס Cookie/HttpContext.User ולא
    // מכיר את סכימת ה-Bearer JWT של האפליקציה, כך שאין דרך פשוטה לאמת גישה אליו בפרודקשן.
    app.MapHangfireDashboard().AllowAnonymous();
}
else
{
    app.UseHsts();

    // בפרודקשן ה-API יושב מאחורי Caddy (reverse proxy) שמטפל ב-TLS ומעביר בקשות פנימיות
    // ב-HTTP רגיל בתוך רשת ה-Docker. בלי זה, UseHttpsRedirection למטה לא מזהה שהבקשה
    // המקורית הייתה HTTPS, ומפנה כל בקשה מחדש בלולאה. הניקוי של הרשתות/פרוקסים הידועים
    // בטוח כאן כי ה-API לא חשוף ישירות לאינטרנט — רק Caddy (שמוסיף X-Forwarded-Proto
    // אוטומטית) יכול להגיע אליו, לפי הגדרת ה-network ב-docker-compose.prod.yml.
    var forwardedHeadersOptions = new ForwardedHeadersOptions
    {
        ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
    };
    forwardedHeadersOptions.KnownIPNetworks.Clear();
    forwardedHeadersOptions.KnownProxies.Clear();
    app.UseForwardedHeaders(forwardedHeadersOptions);
}

// לפני CORS ושאר הצינור, כדי שכל תשובה תקבל את כותרות האבטחה — גם הפניות ותשובות שגיאה.
app.UseSecurityHeaders();

// CORS לפני הפניית HTTPS: בקשת preflight שמגיעה ב-HTTP הייתה מקבלת 307 לפני
// שנוספות כותרות ה-CORS, והדפדפן אינו עוקב אחרי הפניה ב-preflight — הבקשה נכשלת
// עם "No 'Access-Control-Allow-Origin' header" שנראה כאילו המקור אינו מורשה.
app.UseCors(CorsPolicy);

// מיד אחרי CORS ולפני כל השאר: חריגה שתיתפס כאן תחזור ללקוח כתשובת JSON עם קוד
// תקלה, והכותרות שמתירות לדפדפן לקרוא אותה כבר נקבעו. אחרת היא מגיעה כ-500 ריק
// שנראה בצד הלקוח בדיוק כמו שרת מת.
app.UseErrorLog();

app.UseHttpsRedirection();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.ScheduleRecurringJobs();

app.MapControllers();

app.Run();
