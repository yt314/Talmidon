using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Talmidon.Domain.Entities;

namespace Talmidon.Infrastructure.Data.Configurations;

internal sealed class ContactRequestConfiguration : IEntityTypeConfiguration<ContactRequest>
{
    public void Configure(EntityTypeBuilder<ContactRequest> e)
    {
        e.Property(c => c.FullName).HasMaxLength(200).IsRequired();
        e.Property(c => c.Phone).HasMaxLength(40).IsRequired();
        e.Property(c => c.Email).HasMaxLength(256);
        e.Property(c => c.Subject).HasMaxLength(100);
        e.Property(c => c.Message).HasMaxLength(2000).IsRequired();

        e.HasOne(c => c.Teacher)
            .WithMany()
            .HasForeignKey(c => c.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        // המסך פותח על הפניות החדשות, מהחדשה לישנה
        e.HasIndex(c => new { c.TenantId, c.Status, c.CreatedAt });
    }
}
