using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Talmidon.Domain.Entities;

namespace Talmidon.Infrastructure.Data.Configurations;

internal sealed class StudentResourceConfiguration : IEntityTypeConfiguration<StudentResource>
{
    public void Configure(EntityTypeBuilder<StudentResource> e)
    {
        e.Property(r => r.Title).HasMaxLength(200).IsRequired();
        e.Property(r => r.Url).HasMaxLength(2000).IsRequired();
        e.Property(r => r.Description).HasMaxLength(1000);

        e.HasOne(r => r.Teacher)
            .WithMany()
            .HasForeignKey(r => r.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        // קישור לתלמיד — באותו דייר בלבד (מפתח זר מורכב, כמו Note)
        e.HasOne(r => r.Student)
            .WithMany(s => s.Resources)
            .HasForeignKey(r => new { r.StudentId, r.TenantId })
            .HasPrincipalKey(s => new { s.Id, s.TenantId })
            .OnDelete(DeleteBehavior.Cascade);

        e.HasIndex(r => r.StudentId);
    }
}
