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
var frontendOrigin = builder.Environment.IsDevelopment()
    ? "http://localhost:4200"
    : Environment.GetEnvironmentVariable("APP_CLIENT_URL")
        ?? throw new InvalidOperationException("APP_CLIENT_URL must be configured for production CORS.");

// builder.Services.AddCors(options => options.AddPolicy(CorsPolicy, policy =>
// {
//     policy.WithOrigins(frontendOrigin)
//         .AllowAnyHeader()
//         .AllowAnyMethod();
// }));

builder.Services.AddCors(options => options.AddPolicy(CorsPolicy, policy =>
{
    policy.SetIsOriginAllowed(origin =>
            origin == frontendOrigin ||
            (Uri.TryCreate(origin, UriKind.Absolute, out var uri) &&
             uri.Host.EndsWith("talmidon.vercel.app", StringComparison.OrdinalIgnoreCase)))
        .AllowAnyHeader()
        .AllowAnyMethod();
}));

var app = builder.Build();

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

app.UseHttpsRedirection();
app.UseCors(CorsPolicy);
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    // Completely disable Hangfire in production on Render.
    // This ensures the background server and recurring jobs never run outside local development.
    app.UseHangfireServer();

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
}

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
    var email = app.Configuration["Admin:Email"];
    var password = app.Configuration["Admin:Password"];
    if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        return;

    using var scope = app.Services.CreateScope();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    if (await userManager.FindByEmailAsync(email) is not null)
        return;

    var admin = new ApplicationUser { UserName = email, Email = email, DisplayName = "מנהל מערכת", EmailConfirmed = true };
    var createResult = await userManager.CreateAsync(admin, password);
    if (createResult.Succeeded)
        await userManager.AddToRoleAsync(admin, Roles.Admin);
}
