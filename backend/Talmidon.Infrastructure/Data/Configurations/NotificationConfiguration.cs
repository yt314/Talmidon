using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Talmidon.Domain.Entities;

namespace Talmidon.Infrastructure.Data.Configurations;

internal sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> e)
    {
        e.Property(n => n.Type).HasConversion<string>().HasMaxLength(20);
        e.Property(n => n.Title).HasMaxLength(200).IsRequired();
        e.Property(n => n.Message).HasMaxLength(1000).IsRequired();
        e.Property(n => n.LinkPath).HasMaxLength(200);

        e.HasOne(n => n.Teacher)
            .WithMany()
            .HasForeignKey(n => n.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        e.HasIndex(n => new { n.TenantId, n.IsRead });
        e.HasIndex(n => n.CreatedAt);
    }
}
