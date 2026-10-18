using System.Security.Cryptography;

namespace Talmidon.Infrastructure.Diagnostics;

/// <summary>
/// מייצר את הקוד הקצר שמוצג למשתמשת כשמשהו נופל, ושלפיו מוצאים את התקלה במסך הניהול.
/// </summary>
public static class ErrorReference
{
    /// <summary>
    /// בלי האותיות והספרות שמתחלפות זו בזו כשמקריאים קוד בטלפון או מעתיקים אותו
    /// ממסך: I/1, O/0, L, U (שנשמעת כמו V). מה שנשאר אפשר להכתיב בלי לאיית.
    /// </summary>
    private const string Alphabet = "ABCDEFGHJKMNPQRSTVWXYZ23456789";

    private const int Length = 6;

    /// <summary>קוד חדש בצורת <c>ABC-234</c> — המקף רק כדי שיהיה נוח לקרוא ולהקליד.</summary>
    public static string New()
    {
        var characters = new char[Length];
        for (var i = 0; i < Length; i++)
            characters[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];

        return string.Concat(new string(characters, 0, 3), "-", new string(characters, 3, 3));
    }

    /// <summary>
    /// מנרמל קוד שהוקלד ביד לצורה שנשמרה: אותיות גדולות, בלי רווחים, עם מקף במקום.
    /// מי שמקבלת קוד בטלפון מקלידה אותו איך שיוצא, והחיפוש צריך למצוא אותו בכל זאת.
    /// </summary>
    public static string Normalize(string? value)
    {
        var letters = new string((value ?? string.Empty)
            .Where(char.IsLetterOrDigit)
            .Select(char.ToUpperInvariant)
            .ToArray());

        return letters.Length == Length
            ? string.Concat(letters[..3], "-", letters[3..])
            : letters;
    }
}
