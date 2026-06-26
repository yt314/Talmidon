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
    public string? Phone { get; set; }

    /// <summary>תיאור לדף הציבורי.</summary>
    public string? Bio { get; set; }

    /// <summary>מחיר ברירת מחדל לשיעור.</summary>
    public decimal DefaultPricePerLesson { get; set; }

    /// <summary>דף הכללים — כללי ביטול/תשלום.</summary>
    public string? RulesText { get; set; }

    /// <summary>פרטי יצירת קשר לדף הכללים / הספרייה הציבורית.</summary>
    public string? ContactInfo { get; set; }

    /// <summary>האם להציג בספרייה הציבורית.</summary>
    public bool IsPublic { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    // ניווט
    public ICollection<TeacherSubject> Subjects { get; set; } = new List<TeacherSubject>();
    public ICollection<Student> Students { get; set; } = new List<Student>();
    public ICollection<Parent> Parents { get; set; } = new List<Parent>();
    public ICollection<Lesson> Lessons { get; set; } = new List<Lesson>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
    public ICollection<Note> Notes { get; set; } = new List<Note>();
}
