using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Talmidon.Domain.Entities;

namespace Talmidon.Infrastructure.Data.Configurations;

internal sealed class MessageConfiguration : IEntityTypeConfiguration<Message>
{
    public void Configure(EntityTypeBuilder<Message> e)
    {
        e.Property(m => m.Body).HasMaxLength(4000).IsRequired();
        e.Property(m => m.SenderRole).HasConversion<string>().HasMaxLength(20);

        e.HasOne(m => m.Thread)
            .WithMany(t => t.Messages)
            .HasForeignKey(m => new { m.ThreadId, m.TenantId })
            .HasPrincipalKey(t => new { t.Id, t.TenantId })
            .OnDelete(DeleteBehavior.Cascade);

        e.HasIndex(m => new { m.ThreadId, m.CreatedAt });
    }
}
