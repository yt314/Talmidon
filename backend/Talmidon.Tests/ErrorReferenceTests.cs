using Talmidon.Infrastructure.Diagnostics;

namespace Talmidon.Tests;

/// <summary>
/// הקוד הזה נקרא בקול בטלפון ומוקלד בצד השני. כל מה שנבדק כאן הוא שהמסלול הזה עובד.
/// </summary>
public class ErrorReferenceTests
{
    [Fact]
    public void ReferenceIsThreeCharactersHyphenThree()
    {
        var reference = ErrorReference.New();

        Assert.Equal(7, reference.Length);
        Assert.Equal('-', reference[3]);
    }

    /// <summary>
    /// 0/O, 1/I/L ו-U/V הם ההבדל בין קוד שמגיע ליעד לבין שיחה שנייה. הבדיקה רצה
    /// הרבה פעמים כי אות אסורה אחת מתוך שלושים מתגלה רק בהגרלה.
    /// </summary>
    [Fact]
    public void ReferenceNeverUsesCharactersThatLookAlike()
    {
        const string forbidden = "01ILOU";

        for (var i = 0; i < 2000; i++)
        {
            var reference = ErrorReference.New();
            Assert.False(reference.Any(forbidden.Contains), reference);
        }
    }

    [Fact]
    public void TwoReferencesInARowDiffer()
    {
        var references = Enumerable.Range(0, 200).Select(_ => ErrorReference.New()).ToHashSet();

        Assert.Equal(200, references.Count);
    }

    /// <summary>מי שמקבלת קוד בטלפון מקלידה אותו איך שיוצא; החיפוש צריך למצוא אותו בכל זאת.</summary>
    [Theory]
    [InlineData("abc-234", "ABC-234")]
    [InlineData("ABC234", "ABC-234")]
    [InlineData(" abc 234 ", "ABC-234")]
    [InlineData("abc–234", "ABC-234")]
    public void TypedReferencesNormalizeToTheStoredForm(string typed, string expected) =>
        Assert.Equal(expected, ErrorReference.Normalize(typed));

    [Fact]
    public void EmptySearchStaysEmpty() => Assert.Equal("", ErrorReference.Normalize(null));
}
