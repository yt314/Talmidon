using Microsoft.Extensions.Configuration;

namespace Talmidon.Infrastructure.Email;

/// <summary>
/// קישורים אל תוך האפליקציה, למיילים.
///
/// במקום אחד ולא כמחרוזת בכל שולח: כתובת שגויה במייל היא מבוי סתום למי שקיבלה אותו,
/// ושינוי נתיב במסכים חייב מקום אחד לעדכן. כשכתובת הלקוח אינה מוגדרת מוחזר null,
/// והמייל פשוט נשלח בלי כפתור — עדיף מכפתור ששובר.
/// </summary>
public class AppLinks(IConfiguration configuration)
{
    private readonly string _root =
        (configuration["App:ClientUrl"] ?? Environment.GetEnvironmentVariable("APP_CLIENT_URL") ?? "").TrimEnd('/');

    // המורה
    public string? TeacherContactRequests => Of("/app/contact-requests");
    public string? TeacherLessons => Of("/app/lessons");
    public string? TeacherMessages => Of("/app/messages");

    // הורה
    public string? ParentLessons => Of("/parent/lessons");
    public string? ParentPayments => Of("/parent/payments");
    public string? ParentMessages => Of("/parent/messages");

    // תלמידה
    public string? StudentMessages => Of("/student/messages");

    // ניהול
    public string? AdminFeedback => Of("/admin/feedback");

    private string? Of(string path) => string.IsNullOrEmpty(_root) ? null : _root + path;
}
