namespace Talmidon.Domain.Entities;

/// <summary>
/// המורה — שורש הדייר (Tenant). ה-<see cref="Id"/> שלה משמש כ-TenantId לכל הנתונים שבבעלותה.
/// אינה מסוננת ב-Global Query Filter: הפרופיל הציבורי שלה (תחומים, כללים, יצירת קשר)
/// נגיש גם ללא דייר (הספרייה הציבורית). בקרת הגישה אליה נאכפת בשכבת ה-API.
/// </summary>
public class Teacher
{
    public Guid Id { get; set; }

    /// <summary>קישור לחשבון ההתחברות (AspNetUsers).</summary>
    public string UserId { get; set; } = default!;

    public string FullName { get; set; } = default!;

    /// <summary>טלפון ליצירת קשר. מוצג בכרטיס הציבורי כשהמורה בספרייה.</summary>
    public string? Phone { get; set; }

    /// <summary>מייל ליצירת קשר — נפרד מכתובת ההתחברות, ומוצג בכרטיס הציבורי.</summary>
    public string? ContactEmail { get; set; }

    /// <summary>יישוב. מה שהורה מסנן לפיו קודם כל כשהוא מחפש מורה.</summary>
    public string? City { get; set; }

    /// <summary>שכונה בתוך היישוב. מוצגת לצד העיר; אין סינון לפיה.</summary>
    public string? Neighborhood { get; set; }

    /// <summary>
    /// האם המורה פנויה לקבל תלמידות חדשות. מוצג כתג בכרטיס הציבורי.
    ///
    /// מורה שאין לה מקום נראתה עד כה בדיוק כמו מורה פנויה, וההורה גילה את זה רק אחרי
    /// פנייה. ברירת המחדל היא "מקבלת", כדי שמורה קיימת לא תיעלם מהספרייה בשקט.
    /// </summary>
    public bool AcceptingStudents { get; set; } = true;

    /// <summary>תיאור לדף הציבורי.</summary>
    public string? Bio { get; set; }

    /// <summary>מחיר ברירת מחדל לשיעור.</summary>
    public decimal DefaultPricePerLesson { get; set; }

    /// <summary>משך ברירת מחדל לשיעור בדקות (למילוי אוטומטי של שעת הסיום ביומן).</summary>
    public int DefaultDurationMinutes { get; set; } = 60;

    /// <summary>
    /// מחיר לשיעור של 45 דקות. ‎null‎ = לא הוגדר, ואז המחיר נגזר יחסית מהמחיר לשעה.
    ///
    /// שדה נפרד ולא חישוב יחסי תמיד, כי תמחור של מורים אינו לינארי: מי שגובה 140 ₪
    /// לשעה גובה לרוב 120 ₪ ל-45 דקות ולא 105.
    /// </summary>
    public decimal? PricePer45Minutes { get; set; }

    /// <summary>מחיר לשיעור של 30 דקות. ‎null‎ = לא הוגדר (ראו <see cref="PricePer45Minutes"/>).</summary>
    public decimal? PricePer30Minutes { get; set; }

    /// <summary>דף הכללים — כללי ביטול/תשלום.</summary>
    public string? RulesText { get; set; }

    /// <summary>
    /// שדה חופשי ישן ליצירת קשר. הוחלף בטלפון, מייל ועיר, ואינו נערך או מוצג
    /// עוד. העמודה נשארת כדי לא למחוק טקסט שמורות כבר כתבו בה.
    /// </summary>
    public string? ContactInfo { get; set; }

    /// <summary>האם להציג בספרייה הציבורית.</summary>
    public bool IsPublic { get; set; }

    /// <summary>
    /// תמונת פרופיל. נשמרת בבסיס הנתונים ולא בדיסק, כי הפריסה רצה על אחסון בן-חלוף
    /// (הקבצים נמחקים בכל דיפלוי) — התמונה קטנה ומוקטנת בדפדפן לפני השליחה.
    /// <c>null</c> = אין תמונה, והממשק נופל חזרה לראשי תיבות.
    /// </summary>
    public byte[]? PhotoData { get; set; }

    /// <summary>סוג התוכן של <see cref="PhotoData"/> (למשל "image/jpeg"), לצורך ההגשה.</summary>
    public string? PhotoContentType { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    // ניווט
    public ICollection<TeacherSubject> Subjects { get; set; } = new List<TeacherSubject>();
    public ICollection<Student> Students { get; set; } = new List<Student>();
    public ICollection<Parent> Parents { get; set; } = new List<Parent>();
    public ICollection<Lesson> Lessons { get; set; } = new List<Lesson>();
    public ICollection<LessonSeries> LessonSeries { get; set; } = new List<LessonSeries>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
    public ICollection<Note> Notes { get; set; } = new List<Note>();
    public ICollection<TeacherAvailability> Availabilities { get; set; } = new List<TeacherAvailability>();
}
