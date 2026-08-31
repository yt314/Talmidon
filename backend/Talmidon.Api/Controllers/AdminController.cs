using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Talmidon.Api.Contracts;
using Talmidon.Infrastructure.Auth;
using Talmidon.Infrastructure.Data;
using Talmidon.Infrastructure.Identity;

namespace Talmidon.Api.Controllers;

/// <summary>
/// תפקיד-העל המינימלי לתחזוקת הפלטפורמה (ראו נספח האפיון): חוצה-דיירים במפורש (אין TenantId
/// למנהל — לכן כל שאילתה כאן על ישויות בבעלות-מורה משתמשת ב-IgnoreQueryFilters). היקף מכוון:
/// רשימת מורות + נעילה/שחרור של חשבון (למשל בעקבות תלונה) דרך מנגנון ה-Lockout הקיים ב-Identity —
/// אין כאן שכפול/הרחבה של מודל ההרשאות.
/// </summary>
[ApiController]
[Route("api/admin")]
[Authorize(Roles = Roles.Admin)]
public class AdminController(TalmidonDbContext db, UserManager<ApplicationUser> userManager) : ControllerBase
{
    [HttpGet("teachers")]
    public async Task<ActionResult<List<AdminTeacherDto>>> ListTeachers()
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
        var result = teachers.Select(t =>
        {
            users.TryGetValue(t.UserId, out var user);
            var isLockedOut = user?.LockoutEnd is { } end && end > now;
            return new AdminTeacherDto(t.Id, t.FullName, user?.Email ?? "", t.CreatedAt, t.IsPublic, t.StudentCount, isLockedOut);
        }).ToList();

        return Ok(result);
    }

    /// <summary>נועלת את חשבון ההתחברות של המורה (למשל בעקבות תלונה) — משתמשת במנגנון ה-Lockout הקיים של Identity, כך שהתחברות חדשה נחסמת מיידית.</summary>
    [HttpPost("teachers/{id:guid}/lock")]
    public async Task<IActionResult> LockTeacher(Guid id)
    {
        var user = await FindTeacherUserAsync(id);
        if (user is null) return NotFound();

        await userManager.SetLockoutEnabledAsync(user, true);
        await userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);
        return NoContent();
    }

    /// <summary>משחררת נעילה שהוטלה על חשבון מורה.</summary>
    [HttpPost("teachers/{id:guid}/unlock")]
    public async Task<IActionResult> UnlockTeacher(Guid id)
    {
        var user = await FindTeacherUserAsync(id);
        if (user is null) return NotFound();

        await userManager.SetLockoutEndDateAsync(user, null);
        return NoContent();
    }

    private async Task<ApplicationUser?> FindTeacherUserAsync(Guid teacherId)
    {
        var teacher = await db.Teachers.FirstOrDefaultAsync(t => t.Id == teacherId);
        return teacher is null ? null : await userManager.FindByIdAsync(teacher.UserId);
    }
}
