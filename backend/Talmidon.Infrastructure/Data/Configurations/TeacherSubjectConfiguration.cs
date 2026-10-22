using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Talmidon.Domain.Entities;

namespace Talmidon.Infrastructure.Data.Configurations;

internal sealed class TeacherSubjectConfiguration : IEntityTypeConfiguration<TeacherSubject>
{
    public void Configure(EntityTypeBuilder<TeacherSubject> e)
    {
        e.Property(s => s.Name).HasMaxLength(100).IsRequired();

        e.HasOne(s => s.Teacher)
            .WithMany(t => t.Subjects)
            .HasForeignKey(s => s.TeacherId)
            .OnDelete(DeleteBehavior.Cascade);

        // תחום ייחודי לכל מורה (מונע כפילויות בספרייה הציבורית)
        e.HasIndex(s => new { s.TeacherId, s.Name }).IsUnique();
    }
}
