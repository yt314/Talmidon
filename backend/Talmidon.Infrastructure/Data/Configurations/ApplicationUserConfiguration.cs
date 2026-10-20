using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Talmidon.Infrastructure.Identity;

namespace Talmidon.Infrastructure.Data.Configurations;

/// <summary>אכיפת ייחודיות אימייל ברמת המסד (Identity אוכף ברמת ה-UserManager בלבד).</summary>
internal sealed class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> e)
    {
        e.HasIndex(u => u.NormalizedEmail).HasDatabaseName("EmailIndex").IsUnique();
    }
}
