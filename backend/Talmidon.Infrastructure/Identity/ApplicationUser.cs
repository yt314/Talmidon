using Microsoft.AspNetCore.Identity;

namespace Talmidon.Infrastructure.Identity;

/// <summary>
/// משתמש התחברות (מורה / הורה / תלמיד / אדמין). מפתח מסוג string (ברירת מחדל של Identity).
/// פרטי הדומיין (שם מלא, פרטי קשר וכו') נשמרים בישויות Teacher/Parent/Student המקושרות דרך UserId.
/// </summary>
public class ApplicationUser : IdentityUser
{
    public string? DisplayName { get; set; }
}
