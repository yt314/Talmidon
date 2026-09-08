using Talmidon.Domain.Common;
using Talmidon.Domain.Enums;

namespace Talmidon.Domain.Entities;

/// <summary>
/// שיחה בין המורה לבין תלמידה או הורה, סביב תלמידה מסוימת.
///
/// מנגנון אחד ולא שניים: "פנייה למורה" ו"תשובה על הערה" הן אותה שיחה בדיוק, ורק נקודת
/// הפתיחה שונה. כך למורה יש תיבה אחת במקום שלושה מקומות לבדוק, וההורה מקבל את אותה
/// יכולת בלי קוד נוסף.
///
/// אינה מחליפה את הפניות מהספרייה הציבורית (<see cref="ContactRequest"/>): שם מדובר
/// באדם שעדיין אינו לקוח, וכאן בשיחה מתמשכת עם מי שכבר לומד.
/// </summary>
public class MessageThread : ITenantScoped
{
    public Guid Id { get; set; }

    /// <summary>המורה שהשיחה שייכת לה (= TenantId).</summary>
    public Guid TenantId { get; set; }

    /// <summary>התלמידה שהשיחה עוסקת בה — גם כששיחה מתנהלת מול ההורה.</summary>
    public Guid StudentId { get; set; }

    /// <summary>מי מדבר מול המורה בשיחה הזו.</summary>
    public MessageAuthor CounterpartRole { get; set; }

    /// <summary>
    /// מזהה הצד השני — StudentId או ParentId, לפי <see cref="CounterpartRole"/>.
    /// לא מפתח זר, כי הוא מצביע על אחת משתי טבלאות.
    /// </summary>
    public Guid CounterpartId { get; set; }

    public string Subject { get; set; } = default!;

    /// <summary>ההערה שהשיחה נפתחה כתגובה עליה, אם נפתחה כך.</summary>
    public Guid? RelatedNoteId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>מועד ההודעה האחרונה — לפיו מסודרת התיבה. מתעדכן בכל הודעה.</summary>
    public DateTimeOffset LastMessageAt { get; set; }

    /// <summary>
    /// מי שלח את ההודעה האחרונה ותחילת תוכנה. משוכפל כאן בכוונה: התיבה נטענת בכל כניסה
    /// למסך, ובלי זה כל שורה בה הייתה דורשת שאילתת משנה על טבלת ההודעות. הודעה אינה
    /// נערכת ואינה נמחקת, ולכן אין כאן שני מקורות אמת שעלולים להתפצל.
    /// </summary>
    public MessageAuthor LastSenderRole { get; set; }
    public string LastMessagePreview { get; set; } = default!;

    /// <summary>
    /// מתי כל צד קרא אחרון. שני שדות על השיחה במקום סימון לכל הודעה — זה כל מה שצריך
    /// כדי לדעת "יש חדש", בלי טבלה שלישית שגדלה עם כל הודעה.
    /// </summary>
    public DateTimeOffset? TeacherReadAt { get; set; }
    public DateTimeOffset? CounterpartReadAt { get; set; }

    /// <summary>המורה סגרה את השיחה. נשארת לקריאה, ואפשר לפתוח מחדש בהודעה חדשה.</summary>
    public bool IsClosed { get; set; }

    // ניווט
    public Teacher Teacher { get; set; } = default!;
    public Student Student { get; set; } = default!;
    public Note? RelatedNote { get; set; }
    public ICollection<Message> Messages { get; set; } = new List<Message>();
}

/// <summary>הודעה בודדת בתוך שיחה.</summary>
public class Message : ITenantScoped
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid ThreadId { get; set; }

    public MessageAuthor SenderRole { get; set; }

    public string Body { get; set; } = default!;

    public DateTimeOffset CreatedAt { get; set; }

    // ניווט
    public MessageThread Thread { get; set; } = default!;
}
