using System.Text;
using System.Threading.RateLimiting;
using Hangfire;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Talmidon.Api.Multitenancy;
using Talmidon.Infrastructure;
using Talmidon.Infrastructure.Auth;
using Talmidon.Infrastructure.BackgroundJobs;
using Talmidon.Infrastructure.Data;
using Talmidon.Infrastructure.Identity;
using Talmidon.Infrastructure.Multitenancy;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

// שכבת התשתית: DbContext (PostgreSQL), Identity, טוקנים, מיילים
builder.Services.AddInfrastructure(builder.Configuration);

// ספק דייר אמיתי מתוך טוקן ה-JWT (מחליף את NullCurrentTenant)
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentTenant, HttpContextCurrentTenant>();

// אימות JWT
var jwt = builder.Configuration.GetSection("Jwt");
var secret = jwt["SecretKey"]
    ?? throw new InvalidOperationException("Jwt:SecretKey is not configured.");
if (Encoding.UTF8.GetByteCount(secret) < 32)
    throw new InvalidOperationException("Jwt:SecretKey must be at least 32 bytes (256 bits) for HMAC-SHA256.");

var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt["Issuer"],
            ValidateAudience = true,
            ValidAudience = jwt["Audience"],
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = signingKey,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });

// כל נקודת קצה דורשת אימות כברירת מחדל; נקודות ציבוריות מסומנות [AllowAnonymous]
builder.Services.AddAuthorization(options =>
    options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

// הגבלת קצב לנקודות האימות (לפי כתובת IP) — ניתנת לכיוונון בלי קומפילציה מחדש;
// גם מאפשרת להרחיב את המכסה בבדיקות אינטגרציה, שבהן כל הבקשות חולקות "כתובת" מזוהה אחת.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    var permitLimit = builder.Configuration.GetValue("RateLimiting:Auth:PermitLimit", 10);
    var windowMinutes = builder.Configuration.GetValue("RateLimiting:Auth:WindowMinutes", 1);
    options.AddPolicy("auth", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitLimit,
                Window = TimeSpan.FromMinutes(windowMinutes),
                QueueLimit = 0
            }));
});

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

await MigrateDatabaseAsync(app);
await SeedRolesAsync(app);
await SeedAdminUserAsync(app);

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

// CORS לפני הפניית HTTPS: בקשת preflight שמגיעה ב-HTTP הייתה מקבלת 307 לפני
// שנוספות כותרות ה-CORS, והדפדפן אינו עוקב אחרי הפניה ב-preflight — הבקשה נכשלת
// עם "No 'Access-Control-Allow-Origin' header" שנראה כאילו המקור אינו מורשה.
app.UseCors(CorsPolicy);
app.UseHttpsRedirection();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

// המשימות רשומות בכל סביבה: השיעורים החוזרים והתזכורות הם חלק מהמוצר, ובלעדיהן
// המורה צריכה ליצור כל שיעור ביד.
// לוח הבקרה של Hangfire נשאר לפיתוח בלבד; הוא מאמת דרך Cookie ולא מכיר את ה-JWT.
//
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

RecurringJob.AddOrUpdate<LessonReminderJob>(
    "lesson-reminders",
    job => job.RunForAllTenantsAsync(),
    Cron.Hourly(),
    new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });

app.MapControllers();

app.Run();

/// <summary>מחילה מיגרציות ממתינות באתחול — כדי שפריסה (deploy) תהיה "git pull + docker compose up" בלי צעד ידני נפרד.</summary>
static async Task MigrateDatabaseAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<TalmidonDbContext>();
    await db.Database.MigrateAsync();
}

// זריעת התפקידים (Teacher/Parent/Student/Admin) אם חסרים
static async Task SeedRolesAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    foreach (var role in Roles.All)
    {
        if (!await roleManager.RoleExistsAsync(role))
            await roleManager.CreateAsync(new IdentityRole(role));
    }
}

/// <summary>
/// זורעת משתמש-על יחיד לתחזוקת הפלטפורמה, אם מוגדר ב-Admin:Email/Admin:Password. בניגוד למורה/הורה/תלמיד,
/// אין הרשמה עצמית או הזמנה למנהל — זו הדרך היחידה שבה חשבון כזה נוצר, ורק אם הוגדר במפורש (כלומר
/// בפרודקשן חובה להגדיר את שני הערכים ב-secrets/סביבת ההרצה, אחרת לא ייווצר אף חשבון מנהל).
/// </summary>
static async Task SeedAdminUserAsync(WebApplication app)
{
    var email = app.Configuration["Admin:Email"]
        ?? Environment.GetEnvironmentVariable("ADMIN_EMAIL");
    var password = app.Configuration["Admin:Password"]
        ?? Environment.GetEnvironmentVariable("ADMIN_PASSWORD");
    if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        return;

    using var scope = app.Services.CreateScope();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    if (await userManager.FindByEmailAsync(email) is not null)
        return;

    var admin = new ApplicationUser { UserName = email, Email = email, DisplayName = "מנהל מערכת", EmailConfirmed = true };
    var createResult = await userManager.CreateAsync(admin, password);
    if (!createResult.Succeeded)
    {
        var errors = string.Join("; ", createResult.Errors.Select(error => error.Description));
        throw new InvalidOperationException($"Admin user could not be created: {errors}");
    }

    var roleResult = await userManager.AddToRoleAsync(admin, Roles.Admin);
    if (!roleResult.Succeeded)
    {
        var errors = string.Join("; ", roleResult.Errors.Select(error => error.Description));
        throw new InvalidOperationException($"Admin role could not be assigned: {errors}");
    }
}
