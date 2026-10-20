using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Talmidon.Domain.Entities;
using Talmidon.Infrastructure.Identity;

namespace Talmidon.Infrastructure.Data.Configurations;

internal sealed class TeacherConfiguration : IEntityTypeConfiguration<Teacher>
{
    public void Configure(EntityTypeBuilder<Teacher> e)
    {
        e.Property(t => t.FullName).HasMaxLength(200).IsRequired();
        e.Property(t => t.Phone).HasMaxLength(40);
        e.Property(t => t.ContactEmail).HasMaxLength(256);
        e.Property(t => t.City).HasMaxLength(100);
        e.Property(t => t.Neighborhood).HasMaxLength(100);
        e.Property(t => t.AcceptingStudents).HasDefaultValue(true);
        e.Property(t => t.Bio).HasMaxLength(2000);
        e.Property(t => t.RulesText).HasMaxLength(4000);
        e.Property(t => t.ContactInfo).HasMaxLength(1000);
        e.Property(t => t.DefaultPricePerLesson).HasPrecision(10, 2);
        e.Property(t => t.PricePer45Minutes).HasPrecision(10, 2);
        e.Property(t => t.PricePer30Minutes).HasPrecision(10, 2);
        e.Property(t => t.DefaultDurationMinutes).HasDefaultValue(60);
        e.Property(t => t.PhotoContentType).HasMaxLength(100);

        e.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        e.HasIndex(t => t.UserId).IsUnique();
        e.HasIndex(t => t.IsPublic);

        e.ToTable(t =>
        {
            t.HasCheckConstraint(
                "CK_Teachers_DefaultPricePerLesson_NonNegative",
                "\"DefaultPricePerLesson\" >= 0");
            t.HasCheckConstraint(
                "CK_Teachers_PricePer45Minutes_NonNegative",
                "\"PricePer45Minutes\" IS NULL OR \"PricePer45Minutes\" >= 0");
            t.HasCheckConstraint(
                "CK_Teachers_PricePer30Minutes_NonNegative",
                "\"PricePer30Minutes\" IS NULL OR \"PricePer30Minutes\" >= 0");
            t.HasCheckConstraint(
                "CK_Teachers_DefaultDurationMinutes_Positive",
                "\"DefaultDurationMinutes\" > 0");
        });
    }
}
