using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Talmidon.Domain.Entities;

namespace Talmidon.Infrastructure.Data.Configurations;

internal sealed class LessonChangeRequestConfiguration : IEntityTypeConfiguration<LessonChangeRequest>
{
    public void Configure(EntityTypeBuilder<LessonChangeRequest> e)
    {
        e.Property(c => c.Reason).HasMaxLength(1000);
        e.Property(c => c.Type).HasConversion<string>().HasMaxLength(20);
        e.Property(c => c.Status).HasConversion<string>().HasMaxLength(20);

        e.HasOne(c => c.Lesson)
            .WithMany(l => l.ChangeRequests)
            .HasForeignKey(c => new { c.LessonId, c.TenantId })
            .HasPrincipalKey(l => new { l.Id, l.TenantId })
            .OnDelete(DeleteBehavior.Cascade);

        e.HasOne(c => c.RequestedByParent)
            .WithMany()
            .HasForeignKey(c => new { c.RequestedByParentId, c.TenantId })
            .HasPrincipalKey(p => new { p.Id, p.TenantId })
            .OnDelete(DeleteBehavior.Restrict);

        e.HasIndex(c => c.TenantId);
        e.HasIndex(c => c.LessonId);

        e.ToTable(t => t.HasCheckConstraint(
            "CK_LessonChangeRequests_ProposedTime_Order",
            "\"ProposedStartTime\" IS NULL OR \"ProposedEndTime\" IS NULL OR \"ProposedEndTime\" > \"ProposedStartTime\""));
    }
}
