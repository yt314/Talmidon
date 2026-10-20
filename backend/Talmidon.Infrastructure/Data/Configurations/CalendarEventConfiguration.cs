using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Talmidon.Domain.Entities;

namespace Talmidon.Infrastructure.Data.Configurations;

internal sealed class CalendarEventConfiguration : IEntityTypeConfiguration<CalendarEvent>
{
    public void Configure(EntityTypeBuilder<CalendarEvent> e)
    {
        e.Property(c => c.Title).HasMaxLength(200).IsRequired();
        e.Property(c => c.Notes).HasMaxLength(2000);

        e.HasOne(c => c.Teacher)
            .WithMany()
            .HasForeignKey(c => c.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        // היומן נטען תמיד לטווח תאריכים של דייר אחד
        e.HasIndex(c => new { c.TenantId, c.StartTime });
    }
}
