using Talmidon.Infrastructure.Ai;

namespace Talmidon.Tests;

/// <summary>
/// ניקוי המערך שהמודל החזיר.
///
/// הדוגמאות כאן הן מה שהתקבל בפועל מהספק: המורה ראתה ‎\frac{1}{5}‎ ו-‎**מטרות**‎ במסך.
/// ההנחיה מבקשת מהמודל לכתוב טקסט רגיל, וזו הבדיקה שמוודאת שזה מתקיים גם כשלא.
/// </summary>
public class LessonPlanTextTests
{
    [Fact]
    public void FractionsBecomeSomethingATeacherCanRead()
    {
        var cleaned = LessonPlanText.Clean(@"דגמי את הכלל: $\frac{1}{5} + \frac{2}{5}$ שווה שלוש חמישיות.");

        Assert.Equal("דגמי את הכלל: 1/5 + 2/5 שווה שלוש חמישיות.", cleaned);
    }

    [Fact]
    public void NestedFractionsAreUnwrappedToo()
    {
        Assert.Equal("1/2/3", LessonPlanText.Clean(@"\frac{\frac{1}{2}}{3}"));
    }

    [Fact]
    public void BoldMarkersGoAway()
    {
        Assert.Equal("1. מטרות השיעור", LessonPlanText.Clean("**1. מטרות השיעור**"));
    }

    [Fact]
    public void HeadingHashesGoAway()
    {
        Assert.Equal("גוף השיעור", LessonPlanText.Clean("## גוף השיעור"));
    }

    /// <summary>תבליטים מיושרים לסימן אחד, כדי שהרשימה תיראה אותו דבר לכל אורכה.</summary>
    [Fact]
    public void AsteriskBulletsBecomeDashes()
    {
        var cleaned = LessonPlanText.Clean("* פריט ראשון\n* פריט שני\n- פריט שלישי");

        Assert.Equal("- פריט ראשון\n- פריט שני\n- פריט שלישי", cleaned);
    }

    [Fact]
    public void MathSymbolsBecomeTheirEverydayForm()
    {
        Assert.Equal("3 × 4 = 12", LessonPlanText.Clean(@"$3 \times 4 = 12$"));
    }

    /// <summary>פקודה שלא הכרנו יורדת, אבל מה שסביבה נשאר — עדיף חסר מסימן זר.</summary>
    [Fact]
    public void AnUnknownLatexCommandIsDroppedWithoutTakingTheTextWithIt()
    {
        Assert.Equal("שלוש רבעים", LessonPlanText.Clean(@"\qquad שלוש רבעים"));
    }

    /// <summary>
    /// כוכבית בודדת עשויה להיות סימן כפל, ולכן היא אינה נוגעת — רק ההדגשה הכפולה.
    /// </summary>
    [Fact]
    public void ASingleAsteriskInsideALineIsLeftAlone()
    {
        Assert.Equal("התרגיל 3 * 4 נשאר כמו שהוא", LessonPlanText.Clean("התרגיל 3 * 4 נשאר כמו שהוא"));
    }

    /// <summary>
    /// ‎\le‎ הוא תחילתו של ‎\left‎. החלפה לפי מחרוזת הייתה הופכת ‎\left(‎ ל-‎≤ft(‎,
    /// ולכן ההחלפה היא של פקודה שלמה.
    /// </summary>
    [Fact]
    public void ACommandThatStartsWithAnotherIsNotMangled()
    {
        Assert.Equal("( 1/2 ) ≤ 1", LessonPlanText.Clean(@"\left( \frac{1}{2} \right) \le 1"));
    }

    [Fact]
    public void PlainTextComesBackUnchanged()
    {
        const string plan = "1. מטרות השיעור\n- להכיר מכנה משותף\n\n2. פתיחה (10 דקות)";

        Assert.Equal(plan, LessonPlanText.Clean(plan));
    }

    [Fact]
    public void NothingAtAllIsHandled()
    {
        Assert.Equal("", LessonPlanText.Clean(null));
        Assert.Equal("", LessonPlanText.Clean("   "));
    }
}
