using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Talmidon.Domain.Entities;
using Talmidon.Infrastructure.Identity;

namespace Talmidon.Infrastructure.Data.Configurations;

internal sealed class ParentConfiguration : IEntityTypeConfiguration<Parent>
{
    public void Configure(EntityTypeBuilder<Parent> e)
    {
        e.Property(p => p.FullName).HasMaxLength(200).IsRequired();
        e.Property(p => p.Gender).HasConversion<string>().HasMaxLength(10);
        e.Property(p => p.Email).HasMaxLength(256).IsRequired();
        e.Property(p => p.Phone).HasMaxLength(40);

        e.HasAlternateKey(p => new { p.Id, p.TenantId });

        e.HasOne(p => p.Teacher)
            .WithMany(t => t.Parents)
            .HasForeignKey(p => p.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        e.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // אימייל ייחודי לכל מורה (יעד תזכורות/אישורים)
        e.HasIndex(p => new { p.TenantId, p.Email }).IsUnique();
        e.HasIndex(p => p.UserId).IsUnique();
    }
}
