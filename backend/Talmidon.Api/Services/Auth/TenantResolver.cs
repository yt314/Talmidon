using Microsoft.EntityFrameworkCore;
using Talmidon.Infrastructure.Auth;
using Talmidon.Infrastructure.Data;
using Talmidon.Infrastructure.Identity;

namespace Talmidon.Api.Services.Auth;

/// <summary>
/// מאתר את הדייר של המשתמש לפי תפקידו (Teacher → Parent → Student). שימוש ב-IgnoreQueryFilters
/// כי בהתחברות אין עדיין הקשר דייר; SingleOrDefault אוכף מיפוי 1:1 בין משתמש לישות דומיין.
/// </summary>
public class TenantResolver(TalmidonDbContext db)
{
    public async Task<Guid?> ResolveAsync(ApplicationUser user, IList<string> roles)
    {
        if (roles.Contains(Roles.Teacher))
        {
            var teacher = await db.Teachers.IgnoreQueryFilters()
                .SingleOrDefaultAsync(t => t.UserId == user.Id);
            return teacher?.Id;
        }
        if (roles.Contains(Roles.Parent))
        {
            var parent = await db.Parents.IgnoreQueryFilters()
                .SingleOrDefaultAsync(p => p.UserId == user.Id);
            return parent?.TenantId;
        }
        if (roles.Contains(Roles.Student))
        {
            var student = await db.Students.IgnoreQueryFilters()
                .SingleOrDefaultAsync(s => s.UserId == user.Id);
            return student?.TenantId;
        }
        return null;
    }
}
