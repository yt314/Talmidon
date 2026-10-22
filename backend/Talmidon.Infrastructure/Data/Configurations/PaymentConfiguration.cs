using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Talmidon.Domain.Entities;

namespace Talmidon.Infrastructure.Data.Configurations;

internal sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> e)
    {
        e.Property(p => p.Amount).HasPrecision(10, 2);
        e.Property(p => p.Method).HasMaxLength(50);
        e.Property(p => p.Note).HasMaxLength(1000);

        e.HasAlternateKey(p => new { p.Id, p.TenantId });

        e.HasOne(p => p.Teacher)
            .WithMany(t => t.Payments)
            .HasForeignKey(p => p.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        e.HasOne(p => p.Parent)
            .WithMany(par => par.Payments)
            .HasForeignKey(p => new { p.ParentId, p.TenantId })
            .HasPrincipalKey(par => new { par.Id, par.TenantId })
            .OnDelete(DeleteBehavior.Restrict);

        e.HasIndex(p => p.ParentId);

        e.ToTable(t => t.HasCheckConstraint("CK_Payments_Amount_NonNegative", "\"Amount\" >= 0"));
    }
}
