using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Talmidon.Infrastructure.Data;

namespace Talmidon.Tests;

/// <summary>
/// מריץ את ה-API האמיתי (כולל Identity, JWT, Hangfire, ה-DbContext האמיתי) מול בסיס נתונים
/// ייעודי לבדיקות על אותו Postgres של הפיתוח (talmidon_test) — לא Testcontainers ולא מוקים,
/// כדי לבדוק בפועל את ה-Global Query Filters של הבידוד הרב-דיירי ואת שאילתות ה-Npgsql האמיתיות.
///
/// ההגדרות מוזרקות כמשתני סביבה (לא ConfigureAppConfiguration!) לפני שהמארח נבנה: Program.cs
/// קורא כמה ערכים ישירות מ-builder.Configuration בשורות עליונות (top-level statements), עוד
/// לפני ש-WebApplicationFactory מספיקה להחיל את שכבת ה-ConfigureAppConfiguration שלה — וזה
/// גרם בפועל לפער שקט בין המפתח שחתם את ה-JWT (נקרא באיחור, דרך IOptions) למפתח שאימת אותו
/// (נקרא מוקדם, ישירות). משתני סביבה כבר בתוך ה-ConfigurationManager הבסיסי מהרגע הראשון.
/// </summary>
public class TalmidonWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string TestConnectionString =
        "Host=localhost;Port=5432;Database=talmidon_test;Username=talmidon;Password=talmidon_dev_pw";

    public TalmidonWebApplicationFactory()
    {
        Environment.SetEnvironmentVariable("ConnectionStrings__Default", TestConnectionString);
        Environment.SetEnvironmentVariable("Jwt__SecretKey", "test-only-signing-key-not-for-production-0123456789ABCDEF");
        Environment.SetEnvironmentVariable("App__ClientUrl", "http://localhost:4200");
        // TestServer מציג "כתובת" מקור אחת לכל הבקשות, כך שכל הבדיקות המקבילות
        // היו חולקות את אותה מכסת קצב בפועל — מגדילים אותה משמעותית רק כאן.
        Environment.SetEnvironmentVariable("RateLimiting__Auth__PermitLimit", "100000");
        Environment.SetEnvironmentVariable("RateLimiting__Auth__WindowMinutes", "1");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
    }

    /// <summary>
    /// נקרא אוטומטית ע"י xUnit לפני כל הבדיקות באוסף. חייב לרוץ על DbContext עצמאי, לפני כל
    /// גישה ל-Services — גישה ל-Services מפעילה את המארח (כולל SeedRolesAsync ב-Program.cs),
    /// וזה נכשל אם הטבלאות עוד לא קיימות. לכן מריצים מיגרציות ראשונות, ורק אז נוגעים במארח.
    /// </summary>
    public async Task InitializeAsync()
    {
        await EnsureDatabaseExistsAsync();

        var options = new DbContextOptionsBuilder<TalmidonDbContext>()
            .UseNpgsql(TestConnectionString)
            .Options;
        await using var db = new TalmidonDbContext(options, new Talmidon.Infrastructure.Multitenancy.NullCurrentTenant());
        await db.Database.MigrateAsync();
    }

    /// <summary>Migrate() לא יוצר את בסיס הנתונים בפועל אם הוא חסר — יוצרים אותו ידנית דרך בסיס התחזוקה "postgres" אם צריך.</summary>
    private static async Task EnsureDatabaseExistsAsync()
    {
        var builder = new NpgsqlConnectionStringBuilder(TestConnectionString);
        var databaseName = builder.Database!;
        builder.Database = "postgres";

        await using var maintenanceConnection = new NpgsqlConnection(builder.ConnectionString);
        await maintenanceConnection.OpenAsync();

        await using var checkCommand = new NpgsqlCommand("SELECT 1 FROM pg_database WHERE datname = @name", maintenanceConnection);
        checkCommand.Parameters.AddWithValue("name", databaseName);
        if (await checkCommand.ExecuteScalarAsync() is not null) return;

        await using var createCommand = new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\"", maintenanceConnection);
        await createCommand.ExecuteNonQueryAsync();
    }

    async Task IAsyncLifetime.DisposeAsync() => await base.DisposeAsync();
}
