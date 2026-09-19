using System.Text;

namespace Talmidon.Infrastructure.Export;

/// <summary>
/// כתיבת CSV.
///
/// כתוב ביד ולא דרך ספרייה: הקובץ הזה הוא שלוש שורות של כללים, והחשוב בו אינו הפורמט
/// אלא שתי נקודות שספרייה כללית לא הייתה פותרת לבד — עברית שנפתחת נכון באקסל, ותא
/// שמתחיל בסימן שאקסל מפרש כנוסחה.
/// </summary>
public static class Csv
{
    /// <summary>
    /// אקסל קורא קובץ בלי BOM לפי קידוד המערכת, והעברית יוצאת ג'יבריש. שלושת הבתים
    /// האלה הם ההבדל בין קובץ שנפתח לקובץ שמחזיר את המורה לשאול למה זה לא עובד.
    /// </summary>
    public static readonly byte[] Utf8Bom = [0xEF, 0xBB, 0xBF];

    public static byte[] Build(IEnumerable<string> headers, IEnumerable<IEnumerable<string?>> rows)
    {
        var text = new StringBuilder();
        text.Append(Line(headers));
        foreach (var row in rows) text.Append(Line(row));

        return [.. Utf8Bom, .. Encoding.UTF8.GetBytes(text.ToString())];
    }

    private static string Line(IEnumerable<string?> cells) =>
        string.Join(",", cells.Select(Cell)) + "\r\n";

    /// <summary>
    /// תא בודד. מצוטט כשיש בו פסיק, מרכאות או שורה חדשה — ומרכאה בתוכו נכפלת.
    ///
    /// תא שמתחיל ב-‎=‎, ‎+‎, ‎-‎ או ‎@‎ מקבל גרש לפניו: אקסל מריץ תא כזה כנוסחה, ושם של
    /// תלמידה או הערה שהתחילה במקף הופכת שם לשגיאה — ובמקרים אחרים לפקודה.
    /// </summary>
    private static string Cell(string? value)
    {
        var text = (value ?? "").Replace("\r\n", " ").Replace('\n', ' ').Replace('\r', ' ');

        if (text.Length > 0 && text[0] is '=' or '+' or '-' or '@') text = "'" + text;

        return text.Contains(',') || text.Contains('"')
            ? $"\"{text.Replace("\"", "\"\"")}\""
            : text;
    }
}
