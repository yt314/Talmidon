using Microsoft.EntityFrameworkCore;
using Talmidon.Api.Contracts;
using Talmidon.Domain.Entities;
using Talmidon.Domain.Enums;
using Talmidon.Infrastructure.Data;
using Talmidon.Infrastructure.Email;
using Talmidon.Infrastructure.Multitenancy;
using static Talmidon.Api.Services.Lessons.LessonService;

namespace Talmidon.Api.Services.Lessons;

/// <summary>
/// בקשות שמגיעות מהמשפחה (R2/S2) ותשובת המורה עליהן (T6):
/// בקשת שיעור חדש, ובקשת ביטול/שינוי מועד לשיעור קיים.
/// </summary>
public class LessonRequestService(
    TalmidonDbContext db,
    ICurrentTenant currentTenant,
    FamilyMembers family,
    LessonService lessons,
    FamilyNotifier familyNotifier,
    TeacherNotifier teacherNotifier)
{
    private const string LessonsLink = "/app/lessons";

    private Guid TenantId => currentTenant.TenantId
        ?? throw new InvalidOperationException("No tenant in the current context.");

    /// <summary>בקשת שיעור חדש מהורה (נכנס כ-Requested עד אישור המורה).</summary>
    public async Task<ServiceResult<LessonDto>> RequestByParentAsync(string userId, CreateLessonRequest request)
    {
        if (request.EndTime <= request.StartTime) return ServiceError.Invalid(InvalidTimesMessage);

        var parent = await family.ParentAsync(userId);
        if (parent is null || !await family.IsChildOfAsync(parent, request.StudentId)) return ServiceError.Forbidden;

        var student = await db.Students.FirstAsync(s => s.Id == request.StudentId);
        var lesson = lessons.NewLesson(student, request.StartTime, request.EndTime, LessonStatus.Requested, LessonOrigin.Parent);
        lesson.RequestedByParentId = parent.Id;

        return await SubmitLessonRequestAsync(lesson, request.Reason);
    }

    /// <summary>
    /// בקשת שיעור מתלמיד — אותו מסלול כמו של ההורה, והשיעור נכנס כ-Requested עד אישור.
    ///
    /// אין כאן מזהה תלמיד: התלמיד מבקש עבור עצמו בלבד, ולכן אין מה לאמת ואין דרך לבקש
    /// עבור מישהו אחר.
    /// </summary>
    public async Task<ServiceResult<LessonDto>> RequestByStudentAsync(string userId, CreateStudentLessonRequest request)
    {
        if (request.EndTime <= request.StartTime) return ServiceError.Invalid(InvalidTimesMessage);

        var student = await family.StudentAsync(userId);
        if (student is null) return ServiceError.Forbidden;

        var lesson = lessons.NewLesson(student, request.StartTime, request.EndTime, LessonStatus.Requested, LessonOrigin.Student);
        return await SubmitLessonRequestAsync(lesson, request.Reason);
    }

    /// <summary>בקשת ביטול/שינוי מועד לשיעור קיים ומתוזמן.</summary>
    public async Task<ServiceResult<ChangeRequestDto>> RequestChangeAsync(string userId, Guid lessonId, CreateChangeRequestRequest request)
    {
        if (request.Type == ChangeRequestType.Reschedule &&
            (request.ProposedStartTime is null || request.ProposedEndTime is null ||
             request.ProposedEndTime <= request.ProposedStartTime))
            return ServiceError.Invalid("יש לציין מועד חדש תקין לבקשת שינוי.");

        var parent = await family.ParentAsync(userId);
        if (parent is null) return ServiceError.Forbidden;

        var lesson = await db.Lessons.Include(l => l.Student).FirstOrDefaultAsync(l => l.Id == lessonId);
        if (lesson is null) return ServiceError.NotFound();
        if (!await family.IsChildOfAsync(parent, lesson.StudentId)) return ServiceError.Forbidden;

        var conflict = await ChangeRequestConflictAsync(lesson);
        if (conflict is not null) return conflict;

        var changeRequest = NewChangeRequest(lesson, parent, request);
        db.LessonChangeRequests.Add(changeRequest);
        await NotifyTeacherOfChangeRequestAsync(lesson, request.Type);

        return new ChangeRequestDto(
            changeRequest.Id, lesson.Id, lesson.StudentId, lesson.Student.FullName, parent.FullName,
            changeRequest.Type, lesson.StartTime, lesson.EndTime,
            changeRequest.ProposedStartTime, changeRequest.ProposedEndTime,
            changeRequest.Reason, changeRequest.Status, changeRequest.CreatedAt);
    }

    public async Task<ServiceError?> ApproveChangeAsync(Guid id)
    {
        var request = await FindPendingChangeAsync(id);
        if (request.Error is not null) return request.Error;
        var change = request.Value!;
        var lesson = change.Lesson;

        var originalStart = lesson.StartTime;
        if (change.Type == ChangeRequestType.Cancel)
        {
            lesson.Status = LessonStatus.Cancelled;
        }
        else
        {
            lesson.StartTime = change.ProposedStartTime!.Value;
            lesson.EndTime = change.ProposedEndTime!.Value;
        }

        Resolve(change, ChangeRequestStatus.Approved);
        await db.SaveChangesAsync();

        await NotifyChangeApprovedAsync(change, originalStart);
        return null;
    }

    public async Task<ServiceError?> RejectChangeAsync(Guid id)
    {
        var request = await FindPendingChangeAsync(id);
        if (request.Error is not null) return request.Error;
        var change = request.Value!;

        Resolve(change, ChangeRequestStatus.Rejected);
        await db.SaveChangesAsync();

        // דחייה אינה משנה דבר במסכים: השיעור נשאר במועדו והבקשה יורדת מרשימת
        // הממתינות. בלי ההודעה הזו ההורה ששאל פשוט לא מקבל תשובה.
        var studentName = change.Lesson.Student.FullName;
        var lessonStart = change.Lesson.StartTime;
        var what = change.Type == ChangeRequestType.Cancel ? "בקשת הביטול" : "בקשת שינוי המועד";
        await familyNotifier.NotifyParentsAsync(change.Lesson.StudentId,
            EmailSubjects.LessonChangeRequestDeclined(studentName, lessonStart),
            $"{what} לשיעור של {studentName} לא אושרה. השיעור מתקיים כמתוכנן, בתאריך {FormatDate(lessonStart)}.");
        return null;
    }

    // ----- עזר -----

    private async Task<LessonDto> SubmitLessonRequestAsync(Lesson lesson, string? reason)
    {
        db.Lessons.Add(lesson);

        var message = LessonRequestMessage(lesson.Student.FullName, lesson.StartTime, reason);
        teacherNotifier.AddNotification(NotificationType.LessonRequest, "בקשת שיעור חדשה", message, LessonsLink);
        await db.SaveChangesAsync();

        await teacherNotifier.EmailAsync(EmailSubjects.LessonRequest(lesson.Student.FullName, lesson.StartTime), message);
        return ToDto(lesson);
    }

    /// <summary>
    /// נוסח ההודעה למורה. הסיבה שנכתבה בבקשה נכללת בה — עד כה היא התקבלה בשרת ונזרקה,
    /// כך שמי שטרח להסביר למה הוא מבקש שיעור, הסביר לאף אחד.
    /// </summary>
    private static string LessonRequestMessage(string studentName, DateTimeOffset startTime, string? reason)
    {
        var text = $"התקבלה בקשה לקביעת שיעור עבור {studentName} בתאריך {FormatDate(startTime)}.";
        return string.IsNullOrWhiteSpace(reason) ? text : $"{text} סיבה: {reason.Trim()}";
    }

    private async Task<ServiceError?> ChangeRequestConflictAsync(Lesson lesson)
    {
        if (lesson.Status != LessonStatus.Scheduled)
            return ServiceError.Conflict("ניתן לבקש שינוי רק לשיעור מתוזמן.");

        var hasPending = await db.LessonChangeRequests
            .AnyAsync(c => c.LessonId == lesson.Id && c.Status == ChangeRequestStatus.Pending);
        return hasPending ? ServiceError.Conflict("כבר קיימת בקשה ממתינה לשיעור זה.") : null;
    }

    private LessonChangeRequest NewChangeRequest(Lesson lesson, Parent parent, CreateChangeRequestRequest request) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = TenantId,
        LessonId = lesson.Id,
        RequestedByParentId = parent.Id,
        Type = request.Type,
        ProposedStartTime = request.ProposedStartTime,
        ProposedEndTime = request.ProposedEndTime,
        Reason = request.Reason,
        Status = ChangeRequestStatus.Pending,
        CreatedAt = DateTimeOffset.UtcNow
    };

    /// <summary>שומר את הבקשה יחד עם ההתראה, ורק אחר כך שולח מייל.</summary>
    private async Task NotifyTeacherOfChangeRequestAsync(Lesson lesson, ChangeRequestType type)
    {
        var (studentName, start) = (lesson.Student.FullName, lesson.StartTime);
        var title = type == ChangeRequestType.Cancel ? "בקשה לביטול שיעור" : "בקשה לעדכון שיעור";
        var emailSubject = type == ChangeRequestType.Cancel
            ? EmailSubjects.LessonCancelRequest(studentName, start)
            : EmailSubjects.LessonRescheduleRequest(studentName, start);
        var message = $"התקבלה {title} עבור {studentName} (שיעור בתאריך {FormatDate(start)}).";

        teacherNotifier.AddNotification(NotificationType.ChangeRequest, title, message, LessonsLink);
        await db.SaveChangesAsync();
        await teacherNotifier.EmailAsync(emailSubject, message);
    }

    private async Task<ServiceResult<LessonChangeRequest>> FindPendingChangeAsync(Guid id)
    {
        var request = await db.LessonChangeRequests
            .Include(c => c.Lesson).ThenInclude(l => l.Student)
            .FirstOrDefaultAsync(c => c.Id == id);
        if (request is null) return ServiceError.NotFound();
        if (request.Status != ChangeRequestStatus.Pending) return ServiceError.Conflict("הבקשה כבר טופלה.");
        return request;
    }

    private static void Resolve(LessonChangeRequest request, ChangeRequestStatus status)
    {
        request.Status = status;
        request.ResolvedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>המועד בפועל השתנה, ולכן גם התלמידה מקבלת — זה היומן שלה.</summary>
    private Task NotifyChangeApprovedAsync(LessonChangeRequest change, DateTimeOffset originalStart)
    {
        var lesson = change.Lesson;
        var studentName = lesson.Student.FullName;

        return change.Type == ChangeRequestType.Cancel
            ? familyNotifier.NotifyFamilyAsync(lesson.StudentId,
                EmailSubjects.LessonCancelled(studentName, originalStart),
                $"בקשת הביטול אושרה. השיעור של {studentName} בתאריך {FormatDate(originalStart)} בוטל.")
            : familyNotifier.NotifyFamilyAsync(lesson.StudentId,
                EmailSubjects.LessonUpdated(studentName, lesson.StartTime),
                $"בקשת שינוי המועד אושרה. השיעור של {studentName} נקבע ל-{FormatDate(lesson.StartTime)} במקום {FormatDate(originalStart)}.");
    }
}
