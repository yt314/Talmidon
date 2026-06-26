using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Talmidon.Infrastructure.Multitenancy;

namespace Talmidon.Infrastructure.Data;

/// <summary>
/// מפעל זמן-תכן (design-time) ל-<c>dotnet ef</c> — יוצר DbContext להפקת migrations
/// ללא צורך בהרצת ה-API. מחרוזת החיבור נלקחת ממשתנה הסביבה <c>TALMIDON_DB</c>
/// או נופלת לברירת מחדל מקומית (Docker).
/// </summary>
public class TalmidonDbContextFactory : IDesignTimeDbContextFactory<TalmidonDbContext>
{
    public TalmidonDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("TALMIDON_DB")
            ?? "Host=localhost;Port=5432;Database=talmidon;Username=talmidon;Password=talmidon_dev_pw";

        var options = new DbContextOptionsBuilder<TalmidonDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new TalmidonDbContext(options, new NullCurrentTenant());
    }
}
