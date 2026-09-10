using Talmidon.Infrastructure.Email;

namespace Talmidon.Tests;

/// <summary>
/// עיצוב המייל.
///
/// שתי הבדיקות שחייבות להתקיים בכל מייל שיוצא: שהוא לא הופך לדלת לתוכן של מישהו אחר,
/// ושכפתור מוביל למקום אמיתי. השאר — הרכבה של חלקים שקיימים או שאינם.
/// </summary>
public class EmailLayoutTests
{
    [Fact]
    public void TheTitleAndTheContentReachTheEmail()
    {
        var html = EmailLayout.Render(new EmailMessage(
            "פנייה חדשה",
            Greeting: "שלום רבקה,",
            Intro: "התקבלה פנייה חדשה.",
            Details: [("שם", "רותם לוי"), ("טלפון", "050-1234567")],
            Quote: "אשמח לשמוע על שיעורים"));

        Assert.Contains("פנייה חדשה", html);
        Assert.Contains("שלום רבקה,", html);
        Assert.Contains("רותם לוי", html);
        Assert.Contains("050-1234567", html);
        Assert.Contains("אשמח לשמוע על שיעורים", html);
        Assert.Contains("dir=\"rtl\"", html);
    }

    /// <summary>
    /// שם, הודעה ותחום מגיעים מטופס ציבורי. טקסט של משתמש שנכנס כ-HTML הוא איך שמייל
    /// הופך לכלי של מישהו אחר, ולכן כל ערך מקודד — גם כשהוא נראה תמים.
    /// </summary>
    [Fact]
    public void UserTextIsEncodedAndNeverRenderedAsMarkup()
    {
        var html = EmailLayout.Render(new EmailMessage(
            "פנייה",
            Details: [("שם", "<script>alert(1)</script>")],
            Quote: "<img src=x onerror=alert(2)>"));

        Assert.DoesNotContain("<script>", html);
        Assert.DoesNotContain("<img src=x", html);
        Assert.Contains("&lt;script&gt;", html);
    }

    [Fact]
    public void AButtonAppearsWhenThereIsSomewhereToGo()
    {
        var html = EmailLayout.Render(new EmailMessage(
            "פנייה", ActionLabel: "לצפייה בפנייה", ActionUrl: "https://talmidon.app/app/contact-requests"));

        Assert.Contains("לצפייה בפנייה", html);
        Assert.Contains("href=\"https://talmidon.app/app/contact-requests\"", html);
    }

    /// <summary>
    /// כשכתובת הלקוח אינה מוגדרת בשרת אין לאן לקשר. מייל בלי כפתור עדיף על כפתור שבור.
    /// </summary>
    [Fact]
    public void WithoutAnAddressTheEmailSimplyHasNoButton()
    {
        var html = EmailLayout.Render(new EmailMessage("פנייה", ActionLabel: "לצפייה בפנייה", ActionUrl: null));

        Assert.DoesNotContain("לצפייה בפנייה", html);
    }

    /// <summary>
    /// רק http/https. כתובת מסוג אחר בתוך מייל היא דרך להריץ משהו אצל מי שלוחצת, ואין
    /// כאן שום שימוש שמצדיק אותה.
    /// </summary>
    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("file:///etc/passwd")]
    [InlineData("/app/contact-requests")]
    [InlineData("not a url")]
    public void AnAddressThatIsNotAWebAddressIsNotLinked(string url)
    {
        var html = EmailLayout.Render(new EmailMessage("פנייה", ActionLabel: "לחיצה", ActionUrl: url));

        Assert.DoesNotContain("לחיצה", html);
        Assert.DoesNotContain(url, html);
    }

    /// <summary>שורת פרט בלי ערך אינה מוצגת — "מייל:" ריק הוא רעש, לא מידע.</summary>
    [Fact]
    public void AnEmptyDetailIsLeftOut()
    {
        var html = EmailLayout.Render(new EmailMessage(
            "פנייה", Details: [("שם", "רותם"), ("מייל", ""), ("תחום", "   ")]));

        Assert.Contains("שם", html);
        Assert.DoesNotContain("מייל", html);
        Assert.DoesNotContain("תחום", html);
    }

    [Fact]
    public void AListOfLinesBecomesAList()
    {
        var html = EmailLayout.Render(new EmailMessage(
            "תזכורת", Items: ["דנה — 12/03/2026", "איתי — 13/03/2026"]));

        Assert.Contains("<ul", html);
        Assert.Contains("דנה — 12/03/2026", html);
        Assert.Contains("איתי — 13/03/2026", html);
    }

    /// <summary>
    /// לקוחות דואר מתעלמים מגיליונות סגנון ומפריסות מודרניות. סגנון בשורה וטבלאות הם
    /// מה שמחזיק, ובלעדיהם המייל נשבר לערימת טקסט אצל חלק מהמקבלות.
    /// </summary>
    [Fact]
    public void TheLayoutStaysInsideWhatEmailClientsRender()
    {
        var html = EmailLayout.Render(new EmailMessage("נושא", Intro: "תוכן"));

        Assert.DoesNotContain("<style", html);
        Assert.DoesNotContain("display:flex", html);
        Assert.DoesNotContain("display:grid", html);
        Assert.Contains("<table", html);
    }
}
