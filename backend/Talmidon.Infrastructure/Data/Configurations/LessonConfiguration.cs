using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Talmidon.Domain.Entities;

namespace Talmidon.Infrastructure.Data.Configurations;

internal sealed class LessonConfiguration : IEntityTypeConfiguration<Lesson>
{
    public void Configure(EntityTypeBuilder<Lesson> e)
    {
        e.Property(l => l.Homework).HasMaxLength(2000);
        e.Property(l => l.Amount).HasPrecision(10, 2);
        e.Property(l => l.Status).HasConversion<string>().HasMaxLength(20);
        e.Property(l => l.Origin).HasConversion<string>().HasMaxLength(20);

        e.HasAlternateKey(l => new { l.Id, l.TenantId });

        e.HasOne(l => l.Teacher)
            .WithMany(t => t.Lessons)
            .HasForeignKey(l => l.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        // קישור לתלמיד — באותו דייר בלבד (מפתח זר מורכב)
        e.HasOne(l => l.Student)
            .WithMany(s => s.Lessons)
            .HasForeignKey(l => new { l.StudentId, l.TenantId })
            .HasPrincipalKey(s => new { s.Id, s.TenantId })
            .OnDelete(DeleteBehavior.Cascade);

        // הפניות אופציונליות (audit/link) — נשארות חד-עמודה עם SetNull,
        // ובידודן נאכף נוסף ב-SaveChanges (לא ניתן SetNull לעמודת TenantId שאינה nullable).
        e.HasOne(l => l.RequestedByParent)
            .WithMany()
            .HasForeignKey(l => l.RequestedByParentId)
            .OnDelete(DeleteBehavior.SetNull);

        e.HasOne(l => l.Payment)
            .WithMany(p => p.CoveredLessons)
            .HasForeignKey(l => l.PaymentId)
            .OnDelete(DeleteBehavior.SetNull);

        e.HasOne(l => l.Series)
            .WithMany(s => s.Occurrences)
            .HasForeignKey(l => l.SeriesId)
            .OnDelete(DeleteBehavior.SetNull);

        e.HasIndex(l => l.StudentId);
        e.HasIndex(l => l.SeriesId);
        e.HasIndex(l => new { l.TenantId, l.StartTime });
        e.HasIndex(l => l.PaymentId);
        // נתיב חם: שיעורים הפתוחים לתשלום (אינדקס חלקי)
        e.HasIndex(l => new { l.TenantId, l.PaymentRequired })
            .HasFilter("\"PaymentId\" IS NULL");

        e.ToTable(t =>
        {
            t.HasCheckConstraint("CK_Lessons_Amount_NonNegative", "\"Amount\" >= 0");
            t.HasCheckConstraint("CK_Lessons_Time_Order", "\"EndTime\" > \"StartTime\"");
        });
    }
}
