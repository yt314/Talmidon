namespace Talmidon.Domain.Entities;

/// <summary>
/// תחום הוראה של מורה (למשל "מתמטיקה", "אנגלית") — מוצג בספרייה הציבורית.
/// מידע ציבורי, לכן אינו מסונן ב-Global Query Filter.
/// </summary>
public class TeacherSubject
{
    public Guid Id { get; set; }

    /// <summary>המורה הבעלים (= הדייר).</summary>
    public Guid TeacherId { get; set; }

    public string Name { get; set; } = default!;

    public Teacher Teacher { get; set; } = default!;
}
