using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Talmidon.Infrastructure.Auth;
using Talmidon.Infrastructure.Data;
using Talmidon.Infrastructure.Identity;

namespace Talmidon.Api.Startup;

public static class DatabaseInitialization
{
    /// <summary>מיגרציות, תפקידים ומשתמש-העל — מה שצריך להיות במסד לפני שהאפליקציה מקבלת בקשות.</summary>
    public static async Task InitializeDatabaseAsync(this WebApplication app)
    {
        await MigrateDatabaseAsync(app);
        await SeedRolesAsync(app);
        await SeedAdminUserAsync(app);
    }

    /// <summary>מחילה מיגרציות ממתינות באתחול — כדי שפריסה (deploy) תהיה "git pull + docker compose up" בלי צעד ידני נפרד.</summary>
    private static async Task MigrateDatabaseAsync(WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TalmidonDbContext>();
        await db.Database.MigrateAsync();
    }

    // זריעת התפקידים (Teacher/Parent/Student/Admin) אם חסרים
    private static async Task SeedRolesAsync(WebApplication app)
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
    private static async Task SeedAdminUserAsync(WebApplication app)
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
}
