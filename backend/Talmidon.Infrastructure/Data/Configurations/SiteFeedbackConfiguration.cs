using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Talmidon.Domain.Entities;

namespace Talmidon.Infrastructure.Data.Configurations;

internal sealed class SiteFeedbackConfiguration : IEntityTypeConfiguration<SiteFeedback>
{
    public void Configure(EntityTypeBuilder<SiteFeedback> e)
    {
        e.Property(x => x.Message).HasMaxLength(4000).IsRequired();
        e.Property(x => x.ContactInfo).HasMaxLength(256);
        e.Property(x => x.PageUrl).HasMaxLength(500);
        // מסך הניהול פותח על מה שעוד לא טופל, מהחדש לישן
        e.HasIndex(x => new { x.IsHandled, x.CreatedAt });
    }
}
