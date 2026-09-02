namespace Talmidon.Domain.Enums;

/// <summary>סטטוס שיעור לאורך מחזור החיים שלו.</summary>
public enum LessonStatus
{
    /// <summary>הורה ביקש שיעור — ממתין לאישור המורה.</summary>
    Requested = 0,
    /// <summary>שיעור מאושר/קבוע ביומן.</summary>
    Scheduled = 1,
    /// <summary>השיעור התקיים.</summary>
    Completed = 2,
    /// <summary>השיעור בוטל (לאחר שהיה קבוע).</summary>
    Cancelled = 3,
    /// <summary>המורה דחתה בקשת שיעור.</summary>
    Declined = 4,
    /// <summary>התלמיד לא הגיע לשיעור (הבחנה מ"בוטל").</summary>
    NoShow = 5
}

/// <summary>מי יזם את השיעור.</summary>
public enum LessonOrigin
{
    Teacher = 0,
    Parent = 1
}

/// <summary>סוג בקשת שינוי לשיעור קיים.</summary>
public enum ChangeRequestType
{
    Cancel = 0,
    Reschedule = 1
}

/// <summary>סטטוס בקשת שינוי.</summary>
public enum ChangeRequestStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2
}

/// <summary>איך נקבע הסיום של סדרת שיעורים חוזרת.</summary>
public enum LessonSeriesEndCondition
{
    /// <summary>מספר שיעורים קבוע מראש.</summary>
    Count = 0,
    /// <summary>עד תאריך מסוים.</summary>
    EndDate = 1,
    /// <summary>ללא הגבלה — ממשיכה להיווצר עד ביטול ידני.</summary>
    Indefinite = 2
}

/// <summary>
/// מגדר — לצורך ניסוח פנייה מתאים (תלמיד/תלמידה, אבא/אמא). אופציונלי; רשומות ישנות
/// ללא ערך יוצגו בניסוח ניטרלי.
/// </summary>
public enum Gender
{
    Male = 0,
    Female = 1
}

/// <summary>סוג התראה במרכז ההתראות של המורה (לבחירת אייקון/הקשר).</summary>
public enum NotificationType
{
    /// <summary>הורה ביקש שיעור חדש.</summary>
    LessonRequest = 0,
    /// <summary>הורה ביקש שינוי/ביטול לשיעור קיים.</summary>
    ChangeRequest = 1,
    /// <summary>התראה כללית.</summary>
    General = 2,
    /// <summary>פנייה חדשה מהספרייה הציבורית.</summary>
    ContactRequest = 3
}

/// <summary>מצב הטיפול בפנייה שהגיעה מהספרייה הציבורית.</summary>
public enum ContactRequestStatus
{
    /// <summary>הגיעה וטרם נגעו בה.</summary>
    New = 0,
    /// <summary>המורה יצרה קשר והפנייה בטיפול.</summary>
    Handled = 1,
    /// <summary>נסגרה — הפכה לתלמיד, או שלא הבשילה.</summary>
    Closed = 2
}
