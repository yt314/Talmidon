using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Talmidon.Domain.Entities;

namespace Talmidon.Infrastructure.Data.Configurations;

internal sealed class NoteConfiguration : IEntityTypeConfiguration<Note>
{
    public void Configure(EntityTypeBuilder<Note> e)
    {
        e.Property(n => n.Content).HasMaxLength(4000).IsRequired();

        e.HasOne(n => n.Teacher)
            .WithMany(t => t.Notes)
            .HasForeignKey(n => n.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        e.HasOne(n => n.Student)
            .WithMany(s => s.Notes)
            .HasForeignKey(n => new { n.StudentId, n.TenantId })
            .HasPrincipalKey(s => new { s.Id, s.TenantId })
            .OnDelete(DeleteBehavior.Cascade);

        // קישור אופציונלי לשיעור — חד-עמודה עם SetNull
        e.HasOne(n => n.Lesson)
            .WithMany(l => l.Notes)
            .HasForeignKey(n => n.LessonId)
            .OnDelete(DeleteBehavior.SetNull);

        e.HasIndex(n => n.StudentId);
    }
}
