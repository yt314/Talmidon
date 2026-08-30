using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Talmidon.Infrastructure.Auth;
using Talmidon.Infrastructure.BackgroundJobs;
using Talmidon.Infrastructure.Data;
using Talmidon.Infrastructure.Email;
using Talmidon.Infrastructure.Identity;
using Talmidon.Infrastructure.Multitenancy;
using Talmidon.Infrastructure.Scheduling;

namespace Talmidon.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// רושם את שכבת התשתית: DbContext (PostgreSQL), Identity, שירות טוקנים, שולח מיילים,
    /// וספק דייר ברירת מחדל. ה-API מחליף את ספק הדייר בקריאה מתוך ה-JWT (HttpContext).
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException(
                "Connection string 'Default' is not configured. Set it via appsettings.Development.json (dev) " +
                "or the ConnectionStrings__Default environment variable (prod).");

        services.AddDbContext<TalmidonDbContext>(options =>
            options
                .UseNpgsql(connectionString)
                .ConfigureWarnings(w => w.Ignore(
                    CoreEventId.PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning)));

        services.AddScoped<ICurrentTenant, NullCurrentTenant>();

        // הגדרות
        services.Configure<JwtSettings>(configuration.GetSection("Jwt"));
        services.Configure<EmailSettings>(configuration.GetSection("Email"));
        services.Configure<SendGridSettings>(configuration.GetSection("SendGrid"));

        // שירותי אימות
        services.AddScoped<ITokenService, TokenService>();

        // שליחת מייל: SendGrid כש-SendGrid:ApiKey מוגדר (פרודקשן, דרך משתנה הסביבה SendGrid__ApiKey),
        // אחרת SMTP מול Mailpit (ברירת המחדל בפיתוח/בדיקות).
        var sendGridApiKey = configuration["SendGrid:ApiKey"];
        if (!string.IsNullOrWhiteSpace(sendGridApiKey))
            services.AddScoped<IEmailSender, SendGridEmailSender>();
        else
            services.AddScoped<IEmailSender, SmtpEmailSender>();

        services.AddScoped<IAccountProvisioning, AccountProvisioning>();

        // Identity (ללא קוקיז — API מבוסס טוקנים)
        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.SignIn.RequireConfirmedEmail = true;
                options.Password.RequiredLength = 8;
                options.Password.RequireNonAlphanumeric = false;
                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<TalmidonDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders()
            .AddErrorDescriber<HebrewIdentityErrorDescriber>();

        // תוקף קצר יותר לאסימוני אימות מייל (ברירת מחדל של Identity היא יממה)
        services.Configure<DataProtectionTokenProviderOptions>(o => o.TokenLifespan = TimeSpan.FromHours(6));

        // עבודת רקע: תזכורת תשלום חודשית, וייצור מתגלגל של שיעורים מסדרות חוזרות (Hangfire, אחסון על אותו Postgres)
        services.AddScoped<MonthlyPaymentReminderJob>();
        services.AddScoped<LessonSeriesGenerator>();
        services.AddScoped<LessonSeriesGenerationJob>();
        services.AddHangfire(config => config
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UsePostgreSqlStorage(o => o.UseNpgsqlConnection(connectionString)));
        services.AddHangfireServer();

        return services;
    }
}
