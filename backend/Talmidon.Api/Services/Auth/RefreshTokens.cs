using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Talmidon.Api.Contracts;
using Talmidon.Infrastructure.Auth;
using Talmidon.Infrastructure.Data;
using Talmidon.Infrastructure.Identity;

namespace Talmidon.Api.Services.Auth;

/// <summary>
/// אסימוני הגישה והרענון: הנפקה, החלפה (rotation) עם זיהוי שימוש חוזר, ושלילה.
/// </summary>
public class RefreshTokens(
    TalmidonDbContext db,
    ITokenService tokenService,
    UserManager<ApplicationUser> userManager,
    TenantResolver tenants,
    ILogger<RefreshTokens> logger)
{
    /// <summary>זוג אסימונים חדש למשתמש שזה עתה הוכיח את זהותו.</summary>
    public async Task<AuthResponse> IssueAsync(ApplicationUser user)
    {
        var roles = await userManager.GetRolesAsync(user);
        var access = tokenService.CreateAccessToken(user, roles, await tenants.ResolveAsync(user, roles));
        var (rawRefresh, entity) = tokenService.CreateRefreshToken(user.Id);
        db.RefreshTokens.Add(entity);
        await db.SaveChangesAsync();
        return new AuthResponse(access, tokenService.AccessTokenExpiry, rawRefresh, user.Email!, roles.ToArray());
    }

    /// <summary>
    /// מחליף אסימון רענון בזוג חדש. אסימון ששומש כבר (נשלל או הוחלף) הוא חשד לגניבה,
    /// ולכן נשללת כל המשפחה של המשתמש. מצב החשבון נבדק מחדש בכל רענון.
    /// </summary>
    public async Task<ServiceResult<AuthResponse>> RotateAsync(string rawToken)
    {
        var hash = tokenService.HashToken(rawToken);
        var stored = await db.RefreshTokens.FirstOrDefaultAsync(r => r.TokenHash == hash);
        if (stored is null) return ServiceError.Unauthorized("אסימון רענון לא תקין.");

        if (!stored.IsActive)
        {
            await RevokeAllAsync(stored.UserId);
            logger.LogWarning("Refresh token reuse detected for user {UserId}; revoked all tokens.", stored.UserId);
            return ServiceError.Unauthorized("אסימון רענון לא תקין או שפג תוקפו.");
        }

        var user = await userManager.FindByIdAsync(stored.UserId);
        if (user is null || await userManager.IsLockedOutAsync(user) || !await userManager.IsEmailConfirmedAsync(user))
        {
            stored.RevokedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
            return ServiceError.Unauthorized("החשבון אינו זמין כעת.");
        }

        var roles = await userManager.GetRolesAsync(user);
        var access = tokenService.CreateAccessToken(user, roles, await tenants.ResolveAsync(user, roles));
        var (rawRefresh, newToken) = tokenService.CreateRefreshToken(user.Id);

        stored.RevokedAt = DateTimeOffset.UtcNow;
        stored.ReplacedByTokenHash = newToken.TokenHash;
        db.RefreshTokens.Add(newToken);
        await db.SaveChangesAsync();

        return new AuthResponse(access, tokenService.AccessTokenExpiry, rawRefresh, user.Email!, roles.ToArray());
    }

    /// <summary>שולל אסימון אחד — רק אם הוא של המבקש. אסימון של מישהו אחר נשאר כמו שהוא, בשקט.</summary>
    public async Task RevokeAsync(string rawToken, string? ownerUserId)
    {
        var hash = tokenService.HashToken(rawToken);
        var stored = await db.RefreshTokens.FirstOrDefaultAsync(r => r.TokenHash == hash);
        if (stored is not { RevokedAt: null } || stored.UserId != ownerUserId) return;

        stored.RevokedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
    }

    /// <summary>שולל את כל האסימונים הפעילים של המשתמש — התחברות מחדש נדרשת בכל מכשיר.</summary>
    public async Task RevokeAllAsync(string userId)
    {
        var now = DateTimeOffset.UtcNow;
        var active = await db.RefreshTokens
            .Where(r => r.UserId == userId && r.RevokedAt == null)
            .ToListAsync();
        foreach (var token in active)
            token.RevokedAt = now;
        await db.SaveChangesAsync();
    }
}
