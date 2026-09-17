namespace Talmidon.Infrastructure.Email;

/// <summary>
/// שורות הנושא של כל מייל שהמערכת שולחת.
///
/// במקום אחד ולא מפוזרות בבקרים, כי הן נקראות יחד: המורה רואה אותן זו לצד זו בתיבה
/// שלה, ומה שקובע אם היא פותחת הוא אם השורה אומרת <em>מי</em> ו<em>מה</em>. "פנייה
/// חדשה מהספרייה" מחייב לפתוח כדי לדעת ממי; "פנייה חדשה מתלמידה מתעניינת: רותם לוי"
/// כבר עונה על זה.
/// </summary>
public static class EmailSubjects
{
    /// <summary>פנייה מהכרטיס הציבורי — מישהי שעדיין אינה תלמידה.</summary>
    public static string ContactRequest(string fullName, string? subject) =>
        Join($"פנייה חדשה מתלמידה מתעניינת: {Clean(fullName)}", Clean(subject));

    public static string LessonRequest(string studentName, DateTimeOffset start) =>
        $"בקשת שיעור חדש עבור {Clean(studentName)} — {Date(start)}";

    public static string LessonCancelRequest(string studentName, DateTimeOffset start) =>
        $"בקשה לביטול השיעור של {Clean(studentName)} — {Date(start)}";

    public static string LessonRescheduleRequest(string studentName, DateTimeOffset start) =>
        $"בקשה לשינוי מועד השיעור של {Clean(studentName)} — {Date(start)}";

    /// <summary>הודעה חדשה בשיחה. שם השולחת קודם לנושא — היא מה שמזהה את השיחה בתיבה.</summary>
    public static string MessageToTeacher(string senderName, string threadSubject) =>
        Trim($"הודעה מ{Clean(senderName)}: {Clean(threadSubject)}");

    public static string MessageToCounterpart(string teacherName, string threadSubject) =>
        Trim(string.IsNullOrWhiteSpace(teacherName)
            ? $"הודעה מהמורה: {Clean(threadSubject)}"
            : $"הודעה מ{Clean(teacherName)}: {Clean(threadSubject)}");

    // ----- מיילים להורים -----

    public static string LessonAdded(string studentName, DateTimeOffset start) =>
        $"נקבע שיעור ל{Clean(studentName)} — {Date(start)} בשעה {Time(start)}";

    public static string LessonUpdated(string studentName, DateTimeOffset start) =>
        $"השיעור של {Clean(studentName)} עודכן — {Date(start)} בשעה {Time(start)}";

    public static string LessonCancelled(string studentName, DateTimeOffset start) =>
        $"השיעור של {Clean(studentName)} בוטל — {Date(start)}";

    /// <summary>תשובת המורה לבקשת שיעור. נשלחת גם לתלמידה עצמה, כשיש לה חשבון.</summary>
    public static string LessonRequestApproved(string studentName, DateTimeOffset start) =>
        $"בקשת השיעור של {Clean(studentName)} אושרה — {Date(start)} בשעה {Time(start)}";

    /// <summary>
    /// "לא אושרה" ולא "נדחתה": השורה הזו נקראת גם ע"י תלמידה שביקשה בעצמה, והיא
    /// על מועד — לא עליה.
    /// </summary>
    public static string LessonRequestDeclined(string studentName, DateTimeOffset start) =>
        $"בקשת השיעור של {Clean(studentName)} לא אושרה — {Date(start)} בשעה {Time(start)}";

    /// <summary>תשובה לבקשת ביטול/שינוי מועד לשיעור קיים שלא אושרה — השיעור נשאר במועדו.</summary>
    public static string LessonChangeRequestDeclined(string studentName, DateTimeOffset start) =>
        $"הבקשה לשינוי השיעור של {Clean(studentName)} לא אושרה — {Date(start)} בשעה {Time(start)}";

    /// <summary>שיעור שהתלמיד/ה לא הגיע/ה אליו. שורה שהורה צריך לראות בלי לפתוח.</summary>
    public static string LessonNoShow(string studentName, DateTimeOffset start) =>
        $"{Clean(studentName)} לא הגיע/ה לשיעור — {Date(start)} בשעה {Time(start)}";

    /// <summary>
    /// הפסקת שיעור קבוע שגם מחקה שיעורים שכבר נקבעו. המספר בשורת הנושא, כי זה
    /// ההבדל בין "שיעור אחד ירד" לבין "כל החודש הבא נמחק".
    /// </summary>
    public static string LessonSeriesCancelled(string studentName, int cancelledCount) =>
        cancelledCount == 1
            ? $"השיעור הקבוע של {Clean(studentName)} הופסק — שיעור אחד בוטל"
            : $"השיעור הקבוע של {Clean(studentName)} הופסק — {cancelledCount} שיעורים בוטלו";

    /// <summary>תזכורת יכולה לכסות כמה ילדים; אז מונים במקום למנות.</summary>
    public static string LessonReminder(IReadOnlyList<(string StudentName, DateTimeOffset Start)> lessons)
    {
        if (lessons.Count == 0) return "תזכורת לשיעורים הקרובים";

        var first = lessons[0];
        return lessons.Count == 1
            ? $"תזכורת: שיעור של {Clean(first.StudentName)} ב-{Date(first.Start)} בשעה {Time(first.Start)}"
            : $"תזכורת: {lessons.Count} שיעורים קרובים, הראשון ב-{Date(first.Start)} בשעה {Time(first.Start)}";
    }

    public static string PaymentReminder(decimal total, int lessonCount) =>
        $"תזכורת תשלום: ₪{Money(total)} עבור {lessonCount} שיעורים";

    public static string PaymentReceived(decimal amount, DateOnly paidDate) =>
        $"אישור תשלום: ₪{Money(amount)} התקבל ב-{paidDate:dd/MM/yyyy}";

    /// <summary>פנייה לניהול האתר. תחילת ההודעה בשורת הנושא חוסכת פתיחה של כל אחת.</summary>
    public static string SiteFeedback(string message) =>
        $"תלמידון — הודעה מהאתר: {Shorten(Clean(message), 60)}";

    // ----- עזר -----

    private static string Date(DateTimeOffset value) => value.ToString("dd/MM/yyyy");
    private static string Time(DateTimeOffset value) => value.ToString("HH:mm");

    /// <summary>סכום עגול נכתב בלי אגורות — "₪400" ולא "₪400.00".</summary>
    private static string Money(decimal amount) =>
        amount == decimal.Truncate(amount) ? ((long)amount).ToString() : amount.ToString("0.00");

    /// <summary>
    /// שורת נושא היא שורה אחת. שבירת שורה בתוכה נחתכת אצל חלק מספקי הדואר ומקלקלת
    /// את הכותרת אצל אחרים, ולכן כל רווח לבן מתקפל לרווח יחיד.
    /// </summary>
    private static string Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "" : string.Join(" ", value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static string Shorten(string value, int max) =>
        value.Length <= max ? value : value[..max].TrimEnd() + "…";

    private static string Join(string head, string tail) =>
        string.IsNullOrEmpty(tail) ? head : $"{head} — {tail}";

    private static string Trim(string value) => value.TrimEnd(' ', ':', '—', '-');
}
