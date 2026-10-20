using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Talmidon.Domain.Entities;

namespace Talmidon.Infrastructure.Data.Configurations;

internal sealed class LessonSeriesConfiguration : IEntityTypeConfiguration<LessonSeries>
{
    public void Configure(EntityTypeBuilder<LessonSeries> e)
    {
        e.Property(s => s.DayOfWeek).HasConversion<string>().HasMaxLength(10);
        e.Property(s => s.SkipJewishHolidays).HasDefaultValue(true);

        e.HasAlternateKey(s => new { s.Id, s.TenantId });

        e.HasOne(s => s.Teacher)
            .WithMany(t => t.LessonSeries)
            .HasForeignKey(s => s.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        // קישור לתלמיד — באותו דייר בלבד, ובעקביות עם Lesson.Student: מחיקת תלמיד מוחקת גם את הסדרות שלו.
        e.HasOne(s => s.Student)
            .WithMany(st => st.LessonSeries)
            .HasForeignKey(s => new { s.StudentId, s.TenantId })
            .HasPrincipalKey(st => new { st.Id, st.TenantId })
            .OnDelete(DeleteBehavior.Cascade);

        e.HasIndex(s => new { s.TenantId, s.IsActive });

        e.ToTable(t => t.HasCheckConstraint(
            "CK_LessonSeries_EndCondition",
            "\"EndDate\" IS NULL OR \"OccurrenceCount\" IS NULL"));
    }
}
