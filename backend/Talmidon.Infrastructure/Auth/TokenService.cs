using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Talmidon.Domain.Entities;
using Talmidon.Infrastructure.Identity;

namespace Talmidon.Infrastructure.Auth;

public interface ITokenService
{
    /// <summary>שם התביעה (claim) הנושאת את מזהה הדייר בטוקן.</summary>
    const string TenantClaim = "tenant";

    string CreateAccessToken(ApplicationUser user, IEnumerable<string> roles, Guid? tenantId);

    /// <summary>מייצר אסימון רענון: מחזיר את הערך הגולמי (ללקוח) ואת הישות (לשמירה, מגובבת).</summary>
    (string RawToken, RefreshToken Entity) CreateRefreshToken(string userId);

    string HashToken(string rawToken);

    DateTimeOffset AccessTokenExpiry { get; }
}

public class TokenService : ITokenService
{
    private readonly JwtSettings _jwt;

    public TokenService(IOptions<JwtSettings> jwt) => _jwt = jwt.Value;

    public DateTimeOffset AccessTokenExpiry => DateTimeOffset.UtcNow.AddMinutes(_jwt.AccessTokenMinutes);

    public string CreateAccessToken(ApplicationUser user, IEnumerable<string> roles, Guid? tenantId)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(ClaimTypes.NameIdentifier, user.Id),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty)
        };

        foreach (var role in roles)
            claims.Add(new Claim(ClaimTypes.Role, role));

        if (tenantId is Guid tid)
            claims.Add(new Claim(ITokenService.TenantClaim, tid.ToString()));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.SecretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _jwt.Issuer,
            audience: _jwt.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddMinutes(_jwt.AccessTokenMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public (string RawToken, RefreshToken Entity) CreateRefreshToken(string userId)
    {
        var raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        var entity = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = HashToken(raw),
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(_jwt.RefreshTokenDays)
        };
        return (raw, entity);
    }

    public string HashToken(string rawToken)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
}
