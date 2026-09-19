using System.Text;
using Talmidon.Infrastructure.Export;

namespace Talmidon.Tests;

/// <summary>
/// ייצוא CSV. שתי הבדיקות שבאמת חשובות כאן אינן על הפורמט: שהעברית נפתחת באקסל,
/// ושתא לא הופך לנוסחה.
/// </summary>
public class CsvTests
{
    private static string Text(byte[] bytes) => Encoding.UTF8.GetString(bytes);

    [Fact]
    public void TheFileStartsWithABomSoHebrewOpensCorrectly()
    {
        var bytes = Csv.Build(["שם"], [["רותם"]]);

        Assert.Equal(Csv.Utf8Bom, bytes.Take(3));
        Assert.Contains("רותם", Text(bytes));
    }

    [Fact]
    public void HeadersAndRowsComeOutAsLines()
    {
        var text = Text(Csv.Build(["שם", "טלפון"], [["רותם", "050"], ["דנה", "052"]]));

        Assert.Equal("שם,טלפון\r\nרותם,050\r\nדנה,052\r\n", text[1..]);
    }

    [Fact]
    public void ACommaOrAQuoteInsideAValueIsQuoted()
    {
        var text = Text(Csv.Build(["הערה"], [["מתקדמת, אבל מתקשה"], ["אמרה \"מוכנה\""]]));

        Assert.Contains("\"מתקדמת, אבל מתקשה\"", text);
        Assert.Contains("\"אמרה \"\"מוכנה\"\"\"", text);
    }

    /// <summary>
    /// אקסל מריץ תא שמתחיל ב-= כנוסחה. הערה שנכתבה כ"=טוב מאוד" היא לא נוסחה, וזו גם
    /// הדרך המוכרת להחביא פקודה בקובץ שנראה כמו נתונים.
    /// </summary>
    [Theory]
    [InlineData("=1+1")]
    [InlineData("+972501234567")]
    [InlineData("-5 נקודות")]
    [InlineData("@שם")]
    public void AValueExcelWouldTreatAsAFormulaIsNeutralised(string value)
    {
        var text = Text(Csv.Build(["ערך"], [[value]]));

        Assert.Contains("'" + value, text);
    }

    /// <summary>שורה חדשה בתוך תא שוברת את הקובץ לקוראים פשוטים — הערות נכתבות בשורה אחת.</summary>
    [Fact]
    public void ANewlineInsideAValueBecomesASpace()
    {
        var text = Text(Csv.Build(["הערה"], [["שורה ראשונה\nשורה שנייה"]]));

        Assert.Contains("שורה ראשונה שורה שנייה", text);
        Assert.Equal(2, text.Split("\r\n", StringSplitOptions.RemoveEmptyEntries).Length);
    }

    [Fact]
    public void AnEmptyValueIsAnEmptyCell()
    {
        Assert.Equal("א,ב\r\n,\r\n", Text(Csv.Build(["א", "ב"], [[null, ""]]))[1..]);
    }
}
