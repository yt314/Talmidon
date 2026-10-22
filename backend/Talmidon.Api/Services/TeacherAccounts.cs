using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Talmidon.Api.Contracts;
using Talmidon.Infrastructure.Data;
using Talmidon.Infrastructure.Identity;

namespace Talmidon.Api.Services;

/// <summary>
/// חשבונות המורות, מבט המנהל: חוצה-דיירים במפורש (אין TenantId למנהל — ולכן ספירת
/// התלמידים משתמשת ב-IgnoreQueryFilters). נעילה ושחרור דרך מנגנון ה-Lockout הקיים ב-Identity.
/// </summary>
public class TeacherAccounts(TalmidonDbContext db, UserManager<ApplicationUser> userManager)
{
    public async Task<List<AdminTeacherDto>> ListAsync()
    {
        var teachers = await db.Teachers
            .OrderBy(t => t.CreatedAt)
            .Select(t => new
            {
                t.Id,
                t.UserId,
                t.FullName,
                t.CreatedAt,
                t.IsPublic,
                StudentCount = db.Students.IgnoreQueryFilters().Count(s => s.TenantId == t.Id)
            })
            .ToListAsync();

        var userIds = teachers.Select(t => t.UserId).ToList();
        var users = await db.Users
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Email, u.LockoutEnd })
            .ToDictionaryAsync(u => u.Id);

        var now = DateTimeOffset.UtcNow;
        return teachers.Select(t =>
        {
            users.TryGetValue(t.UserId, out var user);
            var isLockedOut = user?.LockoutEnd is { } end && end > now;
            return new AdminTeacherDto(t.Id, t.FullName, user?.Email ?? "", t.CreatedAt, t.IsPublic, t.StudentCount, isLockedOut);
        }).ToList();
    }

    /// <summary>נועלת את חשבון ההתחברות (למשל בעקבות תלונה) — התחברות חדשה נחסמת מיידית.</summary>
    public async Task<ServiceError?> LockAsync(Guid teacherId)
    {
        var user = await FindUserAsync(teacherId);
        if (user is null) return ServiceError.NotFound();

        await userManager.SetLockoutEnabledAsync(user, true);
        await userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);
        return null;
    }

    public async Task<ServiceError?> UnlockAsync(Guid teacherId)
    {
        var user = await FindUserAsync(teacherId);
        if (user is null) return ServiceError.NotFound();

        await userManager.SetLockoutEndDateAsync(user, null);
        return null;
    }

    private async Task<ApplicationUser?> FindUserAsync(Guid teacherId)
    {
        var teacher = await db.Teachers.FirstOrDefaultAsync(t => t.Id == teacherId);
        return teacher is null ? null : await userManager.FindByIdAsync(teacher.UserId);
    }
}
