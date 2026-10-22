using Microsoft.EntityFrameworkCore;
using Talmidon.Api.Contracts;
using Talmidon.Domain.Entities;
using Talmidon.Domain.Enums;
using Talmidon.Infrastructure.Data;
using Talmidon.Infrastructure.Email;
using Talmidon.Infrastructure.Multitenancy;

namespace Talmidon.Api.Services.Lessons;

/// <summary>
/// פעולות המורה על היומן (T4/T5): קביעה, עדכון, מחיקה, סיום שיעור ואישור/דחייה של בקשת שיעור חדש.
/// כל שינוי שהמשפחה צריכה לדעת עליו נשלח אליה מכאן — לא מהבקר.
/// </summary>
public class LessonService(TalmidonDbContext db, ICurrentTenant currentTenant, FamilyNotifier familyNotifier)
{
    public const string InvalidTimesMessage = "שעת הסיום חייבת להיות אחרי שעת ההתחלה.";

    private Guid TenantId => currentTenant.TenantId
        ?? throw new InvalidOperationException("No tenant in the current context.");

    public async Task<ServiceResult<LessonDto>> CreateAsync(CreateLessonRequest request)
    {
        if (request.EndTime <= request.StartTime) return ServiceError.Invalid(InvalidTimesMessage);

        var student = await db.Students.FirstOrDefaultAsync(s => s.Id == request.StudentId);
        if (student is null) return ServiceError.NotFound("תלמיד לא נמצא.");

        var lesson = NewLesson(student, request.StartTime, request.EndTime, LessonStatus.Scheduled, LessonOrigin.Teacher);
        db.Lessons.Add(lesson);
        await db.SaveChangesAsync();

        await familyNotifier.NotifyParentsAsync(student.Id, EmailSubjects.LessonAdded(student.FullName, lesson.StartTime),
            $"נקבע שיעור חדש עבור {student.FullName} בתאריך {FormatDate(lesson.StartTime)}.");

        return ToDto(lesson);
    }

    public async Task<ServiceError?> UpdateAsync(Guid id, UpdateLessonRequest request)
    {
        if (request.EndTime <= request.StartTime) return ServiceError.Invalid(InvalidTimesMessage);

        var lesson = await FindWithStudentAsync(id);
        if (lesson is null) return ServiceError.NotFound();
        if (lesson.Status != LessonStatus.Scheduled) return ServiceError.Conflict("ניתן לעדכן מועד רק לשיעור מתוזמן.");

        lesson.StartTime = request.StartTime;
        lesson.EndTime = request.EndTime;
        await db.SaveChangesAsync();

        await familyNotifier.NotifyParentsAsync(lesson.StudentId, EmailSubjects.LessonUpdated(lesson.Student.FullName, lesson.StartTime),
            $"מועד השיעור של {lesson.Student.FullName} עודכן ל-{FormatDate(lesson.StartTime)}.");
        return null;
    }

    public async Task<ServiceError?> DeleteAsync(Guid id)
    {
        var lesson = await FindWithStudentAsync(id);
        if (lesson is null) return ServiceError.NotFound();
        if (lesson.Status == LessonStatus.Completed) return ServiceError.Conflict("לא ניתן למחוק שיעור שהושלם.");

        var (studentId, studentName, startTime) = (lesson.StudentId, lesson.Student.FullName, lesson.StartTime);
        db.Lessons.Remove(lesson);
        await db.SaveChangesAsync();

        await NotifyCancelledAsync(studentId, studentName, startTime);
        return null;
    }

    /// <summary>T5 — סיום שיעור: סטטוס, תשלום, שיעורי בית, והערה פדגוגית אופציונלית.</summary>
    public async Task<ServiceResult<LessonDto>> CompleteAsync(Guid id, CompleteLessonRequest request)
    {
        if (request.Completed && request.PaymentRequired && request.Amount <= 0)
            return ServiceError.Invalid("יש להזין סכום חיוב.");

        var lesson = await FindWithStudentAsync(id);
        if (lesson is null) return ServiceError.NotFound();
        if (lesson.Status != LessonStatus.Scheduled) return ServiceError.Conflict("ניתן לסמן סיום רק לשיעור מתוזמן.");

        if (request.Completed) MarkCompleted(lesson, request);
        else MarkCancelled(lesson);
        AddLessonNote(lesson, request);
        await db.SaveChangesAsync();

        if (!request.Completed) await NotifyCancelledAsync(lesson.StudentId, lesson.Student.FullName, lesson.StartTime);
        return ToDto(lesson);
    }

    /// <summary>סימון "לא הגיע" — שיעור מתוזמן שהתלמיד לא הופיע אליו (ללא חיוב).</summary>
    public async Task<ServiceResult<LessonDto>> MarkNoShowAsync(Guid id)
    {
        var lesson = await FindWithStudentAsync(id);
        if (lesson is null) return ServiceError.NotFound();
        if (lesson.Status != LessonStatus.Scheduled) return ServiceError.Conflict("ניתן לסמן \"לא הגיע\" רק לשיעור מתוזמן.");

        lesson.Status = LessonStatus.NoShow;
        lesson.PaymentRequired = false;
        lesson.Amount = 0;
        lesson.CompletedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();

        // ההורה הוא מי שאינו יודע. התלמיד/ה היה/הייתה שם או לא, והשיעור נעלם
        // מהמסכים בלי חיוב — כך שבלי ההודעה הזו אין שום סימן שמשהו קרה.
        await familyNotifier.NotifyParentsAsync(lesson.StudentId,
            EmailSubjects.LessonNoShow(lesson.Student.FullName, lesson.StartTime),
            $"{lesson.Student.FullName} לא הגיע/ה לשיעור בתאריך {FormatDate(lesson.StartTime)}. השיעור לא חויב.");

        return ToDto(lesson);
    }

    /// <summary>אישור בקשת שיעור חדש (Origin=Parent/Student, Status=Requested).</summary>
    public async Task<ServiceError?> ApproveRequestAsync(Guid id)
    {
        var lesson = await FindWithStudentAsync(id);
        if (lesson is null) return ServiceError.NotFound();
        if (lesson.Status != LessonStatus.Requested) return ServiceError.Conflict("ניתן לאשר רק בקשת שיעור ממתינה.");

        lesson.Status = LessonStatus.Scheduled;
        await db.SaveChangesAsync();

        var studentName = lesson.Student.FullName;
        await familyNotifier.NotifyFamilyAsync(lesson.StudentId,
            EmailSubjects.LessonRequestApproved(studentName, lesson.StartTime),
            $"בקשת השיעור של {studentName} לתאריך {FormatDate(lesson.StartTime)} אושרה, והשיעור נקבע ביומן.");
        return null;
    }

    /// <summary>
    /// דחיית בקשה. עד לשינוי הזה היא הסתיימה כאן: השיעור ירד מהמסכים של המבקשת,
    /// ומי שביקשה נשארה עם שקט — בלי לדעת אם נדחתה או שהבקשה בכלל לא נקלטה.
    /// </summary>
    public async Task<ServiceError?> DeclineRequestAsync(Guid id)
    {
        var lesson = await FindWithStudentAsync(id);
        if (lesson is null) return ServiceError.NotFound();
        if (lesson.Status != LessonStatus.Requested) return ServiceError.Conflict("ניתן לדחות רק בקשת שיעור ממתינה.");

        lesson.Status = LessonStatus.Declined;
        await db.SaveChangesAsync();

        var studentName = lesson.Student.FullName;
        await familyNotifier.NotifyFamilyAsync(lesson.StudentId,
            EmailSubjects.LessonRequestDeclined(studentName, lesson.StartTime),
            $"המועד שהתבקש עבור {studentName} — {FormatDate(lesson.StartTime)} — אינו מתאים למורה. אפשר לבקש מועד אחר מהיומן.");
        return null;
    }

    // ----- עזר -----

    internal Lesson NewLesson(Student student, DateTimeOffset start, DateTimeOffset end, LessonStatus status, LessonOrigin origin) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = TenantId,
        StudentId = student.Id,
        Student = student,
        StartTime = start,
        EndTime = end,
        Status = status,
        Origin = origin
    };

    internal static LessonDto ToDto(Lesson l) => new(
        l.Id, l.StudentId, l.Student.FullName, l.StartTime, l.EndTime, l.Status, l.Origin,
        l.Homework, l.PaymentRequired, l.Amount, l.PaymentId != null, l.CompletedAt, l.SeriesId);

    /// <summary>מועד שיעור בתוך גוף מייל. שעון ישראל, לא UTC — ראו EmailTime.</summary>
    internal static string FormatDate(DateTimeOffset dt) => EmailTime.DateAndTime(dt);

    private Task<Lesson?> FindWithStudentAsync(Guid id) =>
        db.Lessons.Include(l => l.Student).FirstOrDefaultAsync(l => l.Id == id);

    private Task NotifyCancelledAsync(Guid studentId, string studentName, DateTimeOffset startTime) =>
        familyNotifier.NotifyParentsAsync(studentId, EmailSubjects.LessonCancelled(studentName, startTime),
            $"השיעור של {studentName} בתאריך {FormatDate(startTime)} בוטל.");

    private static void MarkCompleted(Lesson lesson, CompleteLessonRequest request)
    {
        lesson.Status = LessonStatus.Completed;
        lesson.PaymentRequired = request.PaymentRequired;
        lesson.Amount = request.PaymentRequired ? request.Amount : 0;
        lesson.Homework = request.Homework;
        lesson.CompletedAt = DateTimeOffset.UtcNow;
    }

    private static void MarkCancelled(Lesson lesson)
    {
        lesson.Status = LessonStatus.Cancelled;
        lesson.PaymentRequired = false;
        lesson.Amount = 0;
    }

    private void AddLessonNote(Lesson lesson, CompleteLessonRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.NoteContent)) return;

        var note = new Note
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            StudentId = lesson.StudentId,
            LessonId = lesson.Id,
            Content = request.NoteContent,
            CreatedAt = DateTimeOffset.UtcNow
        };
        note.SetVisibility(request.NoteVisibleToStudent, request.NoteVisibleToParent);
        db.Notes.Add(note);
    }
}
