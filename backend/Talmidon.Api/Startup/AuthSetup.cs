using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;

namespace Talmidon.Api.Startup;

public static class AuthSetup
{
    /// <summary>אימות JWT, וכל נקודת קצה דורשת אימות כברירת מחדל; נקודות ציבוריות מסומנות [AllowAnonymous].</summary>
    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var jwt = configuration.GetSection("Jwt");
        var secret = jwt["SecretKey"]
            ?? throw new InvalidOperationException("Jwt:SecretKey is not configured.");
        if (Encoding.UTF8.GetByteCount(secret) < 32)
            throw new InvalidOperationException("Jwt:SecretKey must be at least 32 bytes (256 bits) for HMAC-SHA256.");

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));

        services
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

        services.AddAuthorization(options =>
            options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

        return services;
    }

    /// <summary>
    /// הגבלת קצב לנקודות האימות (לפי כתובת IP) — ניתנת לכיוונון בלי קומפילציה מחדש;
    /// גם מאפשרת להרחיב את המכסה בבדיקות אינטגרציה, שבהן כל הבקשות חולקות "כתובת" מזוהה אחת.
    /// </summary>
    public static IServiceCollection AddAuthRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        return services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            var permitLimit = configuration.GetValue("RateLimiting:Auth:PermitLimit", 10);
            var windowMinutes = configuration.GetValue("RateLimiting:Auth:WindowMinutes", 1);
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
    }
}
