namespace Talmidon.Infrastructure.Auth;

/// <summary>הגדרות JWT (נטענות מ-section "Jwt"). ה-SecretKey הוא סוד — מגיע מ-Dev/env.</summary>
public class JwtSettings
{
    public string Issuer { get; set; } = default!;
    public string Audience { get; set; } = default!;
    public string SecretKey { get; set; } = default!;
    public int AccessTokenMinutes { get; set; } = 60;
    public int RefreshTokenDays { get; set; } = 14;
}
