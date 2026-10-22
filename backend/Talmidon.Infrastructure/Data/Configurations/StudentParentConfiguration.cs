using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Talmidon.Domain.Entities;

namespace Talmidon.Infrastructure.Data.Configurations;

internal sealed class StudentParentConfiguration : IEntityTypeConfiguration<StudentParent>
{
    public void Configure(EntityTypeBuilder<StudentParent> e)
    {
        e.HasKey(sp => new { sp.StudentId, sp.ParentId });

        // קישור באותו דייר בלבד — מפתחות זרים מורכבים
        e.HasOne(sp => sp.Student)
            .WithMany(s => s.StudentParents)
            .HasForeignKey(sp => new { sp.StudentId, sp.TenantId })
            .HasPrincipalKey(s => new { s.Id, s.TenantId })
            .OnDelete(DeleteBehavior.Cascade);

        e.HasOne(sp => sp.Parent)
            .WithMany(p => p.StudentParents)
            .HasForeignKey(sp => new { sp.ParentId, sp.TenantId })
            .HasPrincipalKey(p => new { p.Id, p.TenantId })
            .OnDelete(DeleteBehavior.Cascade);

        e.HasIndex(sp => sp.TenantId);
    }
}
