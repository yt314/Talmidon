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
    Declined = 4
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
