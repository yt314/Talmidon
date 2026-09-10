using Talmidon.Infrastructure.Email;

namespace Talmidon.Tests;

/// <summary>
/// שורות הנושא של המיילים.
///
/// נבדקות כי הן הדבר היחיד שהמורה רואה לפני שהיא מחליטה אם לפתוח. "פנייה חדשה
/// מהספרייה" חייב אותה לפתוח כל אחת כדי לדעת ממי; שורה שאומרת מי ומה חוסכת את זה.
/// </summary>
public class EmailSubjectTests
{
    private static readonly DateTimeOffset Lesson = new(2026, 3, 12, 17, 30, 0, TimeSpan.Zero);

    [Fact]
    public void AContactRequestNamesWhoWroteAndAboutWhat()
    {
        Assert.Equal("פנייה חדשה מתלמידה מתעניינת: רותם לוי — מתמטיקה",
            EmailSubjects.ContactRequest("רותם לוי", "מתמטיקה"));
    }

    /// <summary>בטופס הציבורי התחום אינו חובה, והשורה חייבת להיקרא גם בלעדיו.</summary>
    [Fact]
    public void WithoutASubjectItStillReadsAsASentence()
    {
        Assert.Equal("פנייה חדשה מתלמידה מתעניינת: רותם לוי",
            EmailSubjects.ContactRequest("רותם לוי", null));
    }

    [Fact]
    public void ALessonRequestNamesTheStudentAndTheDate()
    {
        Assert.Equal("בקשת שיעור חדש עבור דנה — 12/03/2026", EmailSubjects.LessonRequest("דנה", Lesson));
    }

    /// <summary>ביטול ושינוי מועד הם שתי בקשות שונות, ואי אפשר לטפל בהן אותו דבר.</summary>
    [Fact]
    public void CancellingAndReschedulingDoNotShareASubject()
    {
        Assert.NotEqual(
            EmailSubjects.LessonCancelRequest("דנה", Lesson),
            EmailSubjects.LessonRescheduleRequest("דנה", Lesson));
        Assert.Contains("ביטול", EmailSubjects.LessonCancelRequest("דנה", Lesson));
        Assert.Contains("שינוי מועד", EmailSubjects.LessonRescheduleRequest("דנה", Lesson));
    }

    [Fact]
    public void AMessageNamesWhoSentIt()
    {
        Assert.Equal("הודעה מרותם: שאלה על שיעורי הבית",
            EmailSubjects.MessageToTeacher("רותם", "שאלה על שיעורי הבית"));
    }

    [Fact]
    public void AMessageFromTheTeacherNamesHerWhenWeKnowIt()
    {
        Assert.Equal("הודעה מרבקה: תזכורת", EmailSubjects.MessageToCounterpart("רבקה", "תזכורת"));
        Assert.Equal("הודעה מהמורה: תזכורת", EmailSubjects.MessageToCounterpart("", "תזכורת"));
    }

    [Fact]
    public void AReminderForOneLessonGivesItsTime()
    {
        Assert.Equal("תזכורת: שיעור של דנה ב-12/03/2026 בשעה 17:30",
            EmailSubjects.LessonReminder([("דנה", Lesson)]));
    }

    /// <summary>תזכורת אחת יכולה לכסות כמה ילדים — אז מונים, ולא מונים שמות בשורת נושא.</summary>
    [Fact]
    public void AReminderForSeveralCountsThemInstead()
    {
        var subject = EmailSubjects.LessonReminder([("דנה", Lesson), ("איתי", Lesson.AddDays(1))]);

        Assert.Equal("תזכורת: 2 שיעורים קרובים, הראשון ב-12/03/2026 בשעה 17:30", subject);
    }

    /// <summary>סכום עגול נכתב בלי אגורות — "₪400" ולא "₪400.00".</summary>
    [Fact]
    public void MoneyIsWrittenTheWayPeopleWriteIt()
    {
        Assert.Equal("אישור תשלום: ₪400 התקבל ב-01/03/2026",
            EmailSubjects.PaymentReceived(400m, new DateOnly(2026, 3, 1)));
        Assert.Contains("₪12.50", EmailSubjects.PaymentReceived(12.5m, new DateOnly(2026, 3, 1)));
    }

    [Fact]
    public void APaymentReminderSaysHowMuchAndForHowMany()
    {
        Assert.Equal("תזכורת תשלום: ₪800 עבור 4 שיעורים", EmailSubjects.PaymentReminder(800m, 4));
    }

    /// <summary>
    /// שורת נושא היא שורה אחת. טקסט חופשי מגיע מטופס ויכול להכיל שורות חדשות, שנחתכות
    /// אצל חלק מספקי הדואר ומקלקלות את הכותרת אצל אחרים.
    /// </summary>
    [Fact]
    public void LineBreaksFromAFormNeverReachTheSubject()
    {
        var subject = EmailSubjects.ContactRequest("רותם\nלוי", "מתמטיקה\r\nופיזיקה");

        Assert.DoesNotContain("\n", subject);
        Assert.DoesNotContain("\r", subject);
        Assert.Equal("פנייה חדשה מתלמידה מתעניינת: רותם לוי — מתמטיקה ופיזיקה", subject);
    }

    [Fact]
    public void ALongFeedbackMessageIsCutToSomethingScannable()
    {
        var subject = EmailSubjects.SiteFeedback(new string('א', 200));

        Assert.StartsWith("תלמידון — הודעה מהאתר: ", subject);
        Assert.EndsWith("…", subject);
        Assert.True(subject.Length < 100);
    }
}
