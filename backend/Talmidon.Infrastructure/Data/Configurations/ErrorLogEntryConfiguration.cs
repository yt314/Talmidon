using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Talmidon.Domain.Entities;

namespace Talmidon.Infrastructure.Data.Configurations;

/// <summary>
/// יומן התקלות. אינו שייך לדייר — הוא נוגע למערכת עצמה, ולכן גם אינו נכנס
/// למסנני הדיירות; הגישה אליו מוגבלת לתפקיד המנהל בשכבת ה-API.
/// </summary>
internal sealed class ErrorLogEntryConfiguration : IEntityTypeConfiguration<ErrorLogEntry>
{
    public void Configure(EntityTypeBuilder<ErrorLogEntry> e)
    {
        e.Property(x => x.Reference).HasMaxLength(16).IsRequired();
        e.Property(x => x.Source).HasMaxLength(40).IsRequired();
        e.Property(x => x.Kind).HasMaxLength(200).IsRequired();
        e.Property(x => x.Message).HasMaxLength(2000).IsRequired();
        e.Property(x => x.Detail).HasMaxLength(4000);
        e.Property(x => x.Path).HasMaxLength(500);
        e.Property(x => x.Method).HasMaxLength(10);
        e.Property(x => x.UserId).HasMaxLength(450);
        e.Property(x => x.UserEmail).HasMaxLength(256);

        // החיפוש היחיד שמתחיל מהמשתמשת הוא "קיבלתי את הקוד הזה"
        e.HasIndex(x => x.Reference).IsUnique();
        // המסך פותח על מה שלא טופל, מהחדש לישן
        e.HasIndex(x => new { x.IsHandled, x.OccurredAt });
        // ניקוי שורות ישנות סורק לפי מועד בלבד
        e.HasIndex(x => x.OccurredAt);
    }
}
