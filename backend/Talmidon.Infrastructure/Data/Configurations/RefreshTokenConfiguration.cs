using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Talmidon.Domain.Entities;
using Talmidon.Infrastructure.Identity;

namespace Talmidon.Infrastructure.Data.Configurations;

internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> e)
    {
        e.Property(r => r.TokenHash).HasMaxLength(128).IsRequired();
        e.Property(r => r.ReplacedByTokenHash).HasMaxLength(128);

        e.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        e.HasIndex(r => r.TokenHash).IsUnique();
        e.HasIndex(r => r.UserId);
    }
}
