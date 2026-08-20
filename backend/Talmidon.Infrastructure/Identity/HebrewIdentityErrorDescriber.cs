using Microsoft.AspNetCore.Identity;

namespace Talmidon.Infrastructure.Identity;

/// <summary>
/// מתרגם את הודעות השגיאה המובנות של ASP.NET Core Identity לעברית, כדי שלא ידלפו
/// הודעות אנגליות גולמיות (למשל "Incorrect password.") לממשק עברי מלא (T10, הרשמה, הזמנות).
/// </summary>
public class HebrewIdentityErrorDescriber : IdentityErrorDescriber
{
    public override IdentityError DefaultError() =>
        new() { Code = nameof(DefaultError), Description = "אירעה שגיאה בלתי צפויה." };

    public override IdentityError ConcurrencyFailure() =>
        new() { Code = nameof(ConcurrencyFailure), Description = "הנתונים השתנו בינתיים. נסה שוב." };

    public override IdentityError PasswordMismatch() =>
        new() { Code = nameof(PasswordMismatch), Description = "הסיסמה הנוכחית שגויה." };

    public override IdentityError InvalidToken() =>
        new() { Code = nameof(InvalidToken), Description = "הקישור אינו תקין או שפג תוקפו." };

    public override IdentityError RecoveryCodeRedemptionFailed() =>
        new() { Code = nameof(RecoveryCodeRedemptionFailed), Description = "קוד השחזור אינו תקין." };

    public override IdentityError LoginAlreadyAssociated() =>
        new() { Code = nameof(LoginAlreadyAssociated), Description = "חשבון עם פרטי כניסה אלו כבר קיים." };

    public override IdentityError InvalidUserName(string? userName) =>
        new() { Code = nameof(InvalidUserName), Description = "שם המשתמש אינו תקין." };

    public override IdentityError InvalidEmail(string? email) =>
        new() { Code = nameof(InvalidEmail), Description = "כתובת האימייל אינה תקינה." };

    public override IdentityError DuplicateUserName(string userName) =>
        new() { Code = nameof(DuplicateUserName), Description = "שם המשתמש כבר תפוס." };

    public override IdentityError DuplicateEmail(string email) =>
        new() { Code = nameof(DuplicateEmail), Description = "כתובת המייל כבר רשומה במערכת." };

    public override IdentityError InvalidRoleName(string? role) =>
        new() { Code = nameof(InvalidRoleName), Description = "שם התפקיד אינו תקין." };

    public override IdentityError DuplicateRoleName(string role) =>
        new() { Code = nameof(DuplicateRoleName), Description = "שם התפקיד כבר קיים." };

    public override IdentityError UserAlreadyInRole(string role) =>
        new() { Code = nameof(UserAlreadyInRole), Description = "המשתמש כבר משויך לתפקיד זה." };

    public override IdentityError UserNotInRole(string role) =>
        new() { Code = nameof(UserNotInRole), Description = "המשתמש אינו משויך לתפקיד זה." };

    public override IdentityError PasswordTooShort(int length) =>
        new() { Code = nameof(PasswordTooShort), Description = $"הסיסמה חייבת להכיל לפחות {length} תווים." };

    public override IdentityError PasswordRequiresUniqueChars(int uniqueChars) =>
        new() { Code = nameof(PasswordRequiresUniqueChars), Description = $"הסיסמה חייבת להכיל לפחות {uniqueChars} תווים ייחודיים." };

    public override IdentityError PasswordRequiresNonAlphanumeric() =>
        new() { Code = nameof(PasswordRequiresNonAlphanumeric), Description = "הסיסמה חייבת להכיל לפחות תו מיוחד אחד." };

    public override IdentityError PasswordRequiresDigit() =>
        new() { Code = nameof(PasswordRequiresDigit), Description = "הסיסמה חייבת להכיל לפחות ספרה אחת." };

    public override IdentityError PasswordRequiresLower() =>
        new() { Code = nameof(PasswordRequiresLower), Description = "הסיסמה חייבת להכיל לפחות אות קטנה אחת (a-z)." };

    public override IdentityError PasswordRequiresUpper() =>
        new() { Code = nameof(PasswordRequiresUpper), Description = "הסיסמה חייבת להכיל לפחות אות גדולה אחת (A-Z)." };

    public override IdentityError UserLockoutNotEnabled() =>
        new() { Code = nameof(UserLockoutNotEnabled), Description = "נעילת חשבון אינה מופעלת עבור משתמש זה." };

    public override IdentityError UserAlreadyHasPassword() =>
        new() { Code = nameof(UserAlreadyHasPassword), Description = "למשתמש כבר מוגדרת סיסמה." };
}
