using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Talmidon.Api.Contracts;
using Talmidon.Domain.Entities;
using Talmidon.Infrastructure.Auth;
using Talmidon.Infrastructure.Data;
using Talmidon.Infrastructure.Identity;
using Talmidon.Infrastructure.Multitenancy;

namespace Talmidon.Api.Services.Students;

/// <summary>שינויים בתלמידים: יצירה (עם כניסה משלה אם יש מייל), עדכון, מחיקה, הזמנה וקישור הורים.</summary>
public class StudentService(
    TalmidonDbContext db,
    ICurrentTenant currentTenant,
    IAccountProvisioning provisioning,
    UserManager<ApplicationUser> userManager)
{
    private Guid TenantId => currentTenant.TenantId
        ?? throw new InvalidOperationException("No tenant in the current context.");

    /// <summary>
    /// תלמיד/ה חדש/ה, וכשיש מייל — גם חשבון כניסה, בעסקה אחת. ההזמנה נשלחת רק אחרי שהכל נשמר.
    /// </summary>
    /// <returns>מזהה התלמיד/ה, או שגיאות ה-Identity של יצירת החשבון.</returns>
    public async Task<(Guid? StudentId, IEnumerable<string>? Errors)> CreateAsync(CreateStudentRequest request)
    {
        await using var transaction = await db.Database.BeginTransactionAsync();

        ApplicationUser? user = null;
        if (!string.IsNullOrWhiteSpace(request.LoginEmail))
        {
            var (created, errors) = await provisioning.CreateInvitedUserAsync(request.LoginEmail, Roles.Student, request.FullName);
            if (created is null)
            {
                await transaction.RollbackAsync();
                return (null, errors);
            }
            user = created;
        }

        var student = NewStudent(request, user);
        db.Students.Add(student);
        await LinkParentsAsync(student.Id, request.ParentIds);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();

        if (user is not null)
            await provisioning.SendInvitationEmailAsync(user, request.FullName);
        return (student.Id, null);
    }

    public async Task<ServiceError?> UpdateAsync(Guid id, UpdateStudentRequest request)
    {
        var student = await db.Students.FirstOrDefaultAsync(s => s.Id == id);
        if (student is null) return ServiceError.NotFound();

        student.FullName = request.FullName;
        student.Gender = request.Gender;
        student.GradeLevel = request.GradeLevel;
        student.BirthDate = request.BirthDate;
        student.GeneralInfo = request.GeneralInfo;
        student.DefaultPricePerLesson = request.DefaultPricePerLesson;
        student.DefaultDurationMinutes = request.DefaultDurationMinutes;
        student.IsActive = request.IsActive;
        await db.SaveChangesAsync();
        return null;
    }

    /// <summary>מוחק את התלמיד/ה ואת חשבון הכניסה שלו/ה. שיעורים, הערות וקישורי הורה נמחקים ב-Cascade.</summary>
    public async Task<ServiceError?> DeleteAsync(Guid id)
    {
        var student = await db.Students.FirstOrDefaultAsync(s => s.Id == id);
        if (student is null) return ServiceError.NotFound();

        var user = student.UserId is not null ? await userManager.FindByIdAsync(student.UserId) : null;

        await using var transaction = await db.Database.BeginTransactionAsync();
        try
        {
            db.Students.Remove(student);
            await db.SaveChangesAsync();
            if (user is not null)
                await userManager.DeleteAsync(user);
            await transaction.CommitAsync();
            return null;
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync();
            return ServiceError.Conflict("לא ניתן למחוק את התלמיד.");
        }
    }

    /// <summary>
    /// Sends the invitation again, for a student who has her own sign-in. The
    /// first one is sent once and its failure is only logged, so a student who
    /// never received it had no way in and the teacher had no way to help.
    /// </summary>
    public async Task<ServiceError?> ResendInvitationAsync(Guid id)
    {
        var student = await db.Students.FirstOrDefaultAsync(s => s.Id == id);
        if (student is null) return ServiceError.NotFound();
        if (student.UserId is null)
            return ServiceError.Conflict("לתלמיד/ה אין כניסה משלו/ה, ולכן אין הזמנה לשלוח.");

        var user = await userManager.FindByIdAsync(student.UserId);
        if (user is null) return ServiceError.NotFound();
        if (user.EmailConfirmed)
            return ServiceError.Conflict("התלמיד/ה כבר קבע/ה סיסמה ואין צורך בהזמנה חדשה.");

        await provisioning.SendInvitationEmailAsync(user, student.FullName);
        return null;
    }

    /// <summary>קישור הורה לתלמיד/ה. קישור שכבר קיים — הצלחה בלי לעשות דבר.</summary>
    public async Task<ServiceError?> LinkParentAsync(Guid studentId, Guid parentId)
    {
        var studentExists = await db.Students.AnyAsync(s => s.Id == studentId);
        var parentExists = await db.Parents.AnyAsync(p => p.Id == parentId);
        if (!studentExists || !parentExists) return ServiceError.NotFound();

        if (await db.StudentParents.AnyAsync(sp => sp.StudentId == studentId && sp.ParentId == parentId))
            return null;

        db.StudentParents.Add(new StudentParent { TenantId = TenantId, StudentId = studentId, ParentId = parentId });
        await db.SaveChangesAsync();
        return null;
    }

    public async Task<ServiceError?> UnlinkParentAsync(Guid studentId, Guid parentId)
    {
        var link = await db.StudentParents.FirstOrDefaultAsync(sp => sp.StudentId == studentId && sp.ParentId == parentId);
        if (link is null) return ServiceError.NotFound();

        db.StudentParents.Remove(link);
        await db.SaveChangesAsync();
        return null;
    }

    private Student NewStudent(CreateStudentRequest request, ApplicationUser? user) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = TenantId,
        UserId = user?.Id,
        FullName = request.FullName,
        Gender = request.Gender,
        GradeLevel = request.GradeLevel,
        BirthDate = request.BirthDate,
        GeneralInfo = request.GeneralInfo,
        DefaultPricePerLesson = request.DefaultPricePerLesson,
        DefaultDurationMinutes = request.DefaultDurationMinutes,
        IsActive = true,
        CreatedAt = DateTimeOffset.UtcNow
    };

    /// <summary>רק הורים של הדייר הנוכחי (מסונן אוטומטית) — מזהה של הורה אחר פשוט לא נמצא.</summary>
    private async Task LinkParentsAsync(Guid studentId, List<Guid>? parentIds)
    {
        if (parentIds is not { Count: > 0 }) return;

        var validParentIds = await db.Parents
            .Where(p => parentIds.Contains(p.Id))
            .Select(p => p.Id)
            .ToListAsync();

        foreach (var parentId in validParentIds)
            db.StudentParents.Add(new StudentParent { TenantId = TenantId, StudentId = studentId, ParentId = parentId });
    }
}
