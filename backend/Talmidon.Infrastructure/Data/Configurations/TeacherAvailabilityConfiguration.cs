using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Talmidon.Domain.Entities;

namespace Talmidon.Infrastructure.Data.Configurations;

internal sealed class TeacherAvailabilityConfiguration : IEntityTypeConfiguration<TeacherAvailability>
{
    public void Configure(EntityTypeBuilder<TeacherAvailability> e)
    {
        e.Property(a => a.DayOfWeek).HasConversion<int>();

        e.HasOne(a => a.Teacher)
            .WithMany(t => t.Availabilities)
            .HasForeignKey(a => a.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        e.HasIndex(a => a.TenantId);

        e.ToTable(t => t.HasCheckConstraint(
            "CK_TeacherAvailabilities_Time_Order",
            "\"EndTime\" > \"StartTime\""));
    }
}
