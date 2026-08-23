using System.Text;
using System.Threading.RateLimiting;
using Hangfire;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Talmidon.Api.Multitenancy;
using Talmidon.Infrastructure;
using Talmidon.Infrastructure.Auth;
using Talmidon.Infrastructure.BackgroundJobs;
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

// הגבלת קצב לנקודות האימות (לפי כתובת IP)
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});

// CORS לאפליקציית ה-Angular
const string CorsPolicy = "TalmidonClient";
builder.Services.AddCors(options => options.AddPolicy(CorsPolicy, policy =>
{
    if (builder.Environment.IsDevelopment())
    {
        // בפיתוח מאפשרים כל פורט על localhost/127.0.0.1 — כלים כמו VS Code Simple Browser
        // מעבירים (proxy) את ה-Angular dev server דרך פורט אקראי, לא בהכרח 4200.
        policy.SetIsOriginAllowed(origin =>
                Uri.TryCreate(origin, UriKind.Absolute, out var uri) && uri.Host is "localhost" or "127.0.0.1")
            .AllowAnyHeader()
            .AllowAnyMethod();
    }
    else
    {
        var clientUrl = builder.Configuration["App:ClientUrl"];
        if (string.IsNullOrWhiteSpace(clientUrl))
            throw new InvalidOperationException("App:ClientUrl must be configured for CORS in non-development environments.");
        policy.WithOrigins(clientUrl).AllowAnyHeader().AllowAnyMethod();
    }
}));

var app = builder.Build();

await SeedRolesAsync(app);

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
}

app.UseHttpsRedirection();
app.UseCors(CorsPolicy);
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// תזכורת תשלום חודשית לכל הדיירים — פעם בחודש, UTC (סעיף 6-7 באפיון).
RecurringJob.AddOrUpdate<MonthlyPaymentReminderJob>(
    "monthly-payment-reminders",
    job => job.RunForAllTenantsAsync(),
    Cron.Monthly(),
    new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });

app.Run();

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
