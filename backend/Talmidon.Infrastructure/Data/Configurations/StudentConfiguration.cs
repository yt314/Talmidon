using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Talmidon.Domain.Entities;
using Talmidon.Infrastructure.Identity;

namespace Talmidon.Infrastructure.Data.Configurations;

internal sealed class StudentConfiguration : IEntityTypeConfiguration<Student>
{
    public void Configure(EntityTypeBuilder<Student> e)
    {
        e.Property(s => s.FullName).HasMaxLength(200).IsRequired();
        e.Property(s => s.Gender).HasConversion<string>().HasMaxLength(10);
        e.Property(s => s.GradeLevel).HasMaxLength(50);
        e.Property(s => s.GeneralInfo).HasMaxLength(4000);
        e.Property(s => s.DefaultPricePerLesson).HasPrecision(10, 2);

        // מפתח חלופי לשמש כיעד למפתחות זרים מורכבים (Id, TenantId)
        e.HasAlternateKey(s => new { s.Id, s.TenantId });

        e.HasOne(s => s.Teacher)
            .WithMany(t => t.Students)
            .HasForeignKey(s => s.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        e.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.SetNull);

        e.HasIndex(s => s.TenantId);
        e.HasIndex(s => s.UserId).IsUnique();

        e.ToTable(t =>
        {
            t.HasCheckConstraint(
                "CK_Students_DefaultPricePerLesson_NonNegative",
                "\"DefaultPricePerLesson\" IS NULL OR \"DefaultPricePerLesson\" >= 0");
            t.HasCheckConstraint(
                "CK_Students_DefaultDurationMinutes_Positive",
                "\"DefaultDurationMinutes\" IS NULL OR \"DefaultDurationMinutes\" > 0");
        });
    }
}
