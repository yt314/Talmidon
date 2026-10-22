using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Talmidon.Api.Contracts;
using Talmidon.Domain.Entities;
using Talmidon.Infrastructure.Auth;
using Talmidon.Infrastructure.Data;
using Talmidon.Infrastructure.Identity;
using Talmidon.Infrastructure.Multitenancy;

namespace Talmidon.Api.Services.Parents;

/// <summary>הורים: קריאה, ויצירה/עדכון/מחיקה יחד עם חשבון הכניסה שלהם.</summary>
public class ParentService(
    TalmidonDbContext db,
    ICurrentTenant currentTenant,
    IAccountProvisioning provisioning,
    UserManager<ApplicationUser> userManager)
{
    private Guid TenantId => currentTenant.TenantId
        ?? throw new InvalidOperationException("No tenant in the current context.");

    public Task<List<ParentDto>> ListAsync() => Projected(db.Parents.OrderBy(p => p.FullName)).ToListAsync();

    public Task<ParentDto?> GetAsync(Guid id) => Projected(db.Parents.Where(p => p.Id == id)).FirstOrDefaultAsync();

    public Task<MyParentProfileDto?> ProfileOfParentAsync(string userId) =>
        db.Parents
            .Where(p => p.UserId == userId)
            .Select(p => new MyParentProfileDto(p.FullName, p.Gender))
            .FirstOrDefaultAsync();

    /// <summary>הורה חדש וחשבון הכניסה שלו בעסקה אחת. ההזמנה נשלחת רק אחרי שהכל נשמר.</summary>
    /// <returns>ההורה, או שגיאות ה-Identity של יצירת החשבון.</returns>
    public async Task<(ParentDto? Parent, IEnumerable<string>? Errors)> CreateAsync(CreateParentRequest request)
    {
        await using var transaction = await db.Database.BeginTransactionAsync();

        var (user, errors) = await provisioning.CreateInvitedUserAsync(request.Email, Roles.Parent, request.FullName);
        if (user is null)
        {
            await transaction.RollbackAsync();
            return (null, errors);
        }

        var parent = new Parent
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            UserId = user.Id,
            FullName = request.FullName,
            Gender = request.Gender,
            Email = request.Email,
            Phone = request.Phone
        };
        db.Parents.Add(parent);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();

        await provisioning.SendInvitationEmailAsync(user, request.FullName);
        return (new ParentDto(parent.Id, parent.FullName, parent.Gender, parent.Email, parent.Phone, 0, false), null);
    }

    /// <summary>
    /// Sends the invitation again. The first one is sent once and its failure
    /// is only logged, so a parent who never received it — a send that failed,
    /// a spam folder, a link that expired — had no way back in and the teacher
    /// had no way to help her.
    /// </summary>
    public async Task<ServiceError?> ResendInvitationAsync(Guid id)
    {
        var parent = await db.Parents.FirstOrDefaultAsync(p => p.Id == id);
        if (parent is null) return ServiceError.NotFound();

        var user = await userManager.FindByIdAsync(parent.UserId);
        if (user is null) return ServiceError.NotFound();
        if (user.EmailConfirmed)
            return ServiceError.Conflict("ההורה כבר קבע סיסמה ואין צורך בהזמנה חדשה.");

        await provisioning.SendInvitationEmailAsync(user, parent.FullName);
        return null;
    }

    /// <summary>המייל אינו משתנה כאן — הוא שם המשתמש של החשבון.</summary>
    public async Task<ServiceError?> UpdateAsync(Guid id, UpdateParentRequest request)
    {
        var parent = await db.Parents.FirstOrDefaultAsync(p => p.Id == id);
        if (parent is null) return ServiceError.NotFound();

        parent.FullName = request.FullName;
        parent.Gender = request.Gender;
        parent.Phone = request.Phone;
        await db.SaveChangesAsync();
        return null;
    }

    /// <summary>
    /// מוחק את ההורה ואת חשבון הכניסה. קישורי תלמיד-הורה נמחקים ב-Cascade; הורה עם
    /// תשלומים חסום ע"י המסד (FK Restrict) — ואז העסקה כולה מתבטלת, גם מחיקת החשבון.
    /// </summary>
    public async Task<ServiceError?> DeleteAsync(Guid id)
    {
        var parent = await db.Parents.FirstOrDefaultAsync(p => p.Id == id);
        if (parent is null) return ServiceError.NotFound();

        var user = await userManager.FindByIdAsync(parent.UserId);

        await using var transaction = await db.Database.BeginTransactionAsync();
        try
        {
            db.Parents.Remove(parent);
            await db.SaveChangesAsync();
            if (user is not null)
                await userManager.DeleteAsync(user);
            await transaction.CommitAsync();
            return null;
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync();
            return ServiceError.Conflict("לא ניתן למחוק הורה עם תשלומים מקושרים.");
        }
    }

    private IQueryable<ParentDto> Projected(IQueryable<Parent> parents) =>
        parents.Select(p => new ParentDto(p.Id, p.FullName, p.Gender, p.Email, p.Phone, p.StudentParents.Count,
            db.Users.Any(u => u.Id == p.UserId && u.EmailConfirmed)));
}
