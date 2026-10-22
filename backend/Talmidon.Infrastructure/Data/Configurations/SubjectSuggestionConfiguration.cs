using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Talmidon.Domain.Entities;

namespace Talmidon.Infrastructure.Data.Configurations;

internal sealed class SubjectSuggestionConfiguration : IEntityTypeConfiguration<SubjectSuggestion>
{
    public void Configure(EntityTypeBuilder<SubjectSuggestion> e)
    {
        e.Property(x => x.Name).HasMaxLength(100).IsRequired();
        // שם אחד בלבד ברשימה, בלי תלות באותיות גדולות/קטנות בצד היישום
        e.HasIndex(x => x.Name).IsUnique();
    }
}
