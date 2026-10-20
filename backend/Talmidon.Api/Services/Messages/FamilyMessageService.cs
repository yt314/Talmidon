using Microsoft.EntityFrameworkCore;
using Talmidon.Api.Contracts;
using Talmidon.Domain.Enums;
using Talmidon.Infrastructure.Data;

namespace Talmidon.Api.Services.Messages;

/// <summary>
/// הצד של התלמידה או ההורה בשיחה. כל פעולה מוגבלת לשיחות של המחובר בלבד —
/// שיחה של מישהו אחר מקבלת NotFound, לא Forbidden, כדי לא לחשוף שהיא קיימת.
/// </summary>
public class FamilyMessageService(
    TalmidonDbContext db,
    MessageThreads threads,
    FamilyMembers family,
    MessageNotifier notifier)
{
    public async Task<ServiceResult<ThreadDetailDto>> OpenAsync(Counterpart me, Guid id)
    {
        var thread = await threads.Of(me)
            .Include(t => t.Messages)
            .FirstOrDefaultAsync(t => t.Id == id);
        if (thread is null) return ServiceError.NotFound();

        thread.CounterpartReadAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();

        return await threads.ToDetailAsync(thread);
    }

    /// <summary>
    /// פנייה חדשה למורה. תלמידה פונה בעניין עצמה ואינה שולחת מזהה; הורה חייב לציין באיזה
    /// ילד מדובר. אפשר לקשור את הפנייה להערה שהתקבלה — כך המורה רואה על מה מגיבים.
    /// </summary>
    public async Task<ServiceResult<ThreadDetailDto>> StartAsync(Counterpart me, StartThreadRequest request)
    {
        var body = MessageThreads.Clean(request.Body);
        var subject = MessageThreads.Clean(request.Subject);
        if (body is null || subject is null) return ServiceError.Invalid("נושא ותוכן הם שדות חובה.");

        var studentId = me.Role == MessageAuthor.Student ? me.Id : request.StudentId;
        if (studentId is null) return ServiceError.Invalid("יש לבחור עבור מי הפנייה.");

        var invalid = await ValidateStartAsync(me, studentId.Value, request.RelatedNoteId);
        if (invalid is not null) return invalid;

        var thread = threads.New(studentId.Value, me, subject, request.RelatedNoteId);
        threads.Append(thread, me.Role, body);
        thread.CounterpartReadAt = thread.LastMessageAt;
        db.MessageThreads.Add(thread);

        await NotifyTeacherAsync(me, thread.StudentId, subject, body);
        return await threads.ToDetailAsync(thread);
    }

    public async Task<ServiceResult<MessageDto>> ReplyAsync(Counterpart me, Guid id, PostMessageRequest request)
    {
        var body = MessageThreads.Clean(request.Body);
        if (body is null) return ServiceError.Invalid("אי אפשר לשלוח הודעה ריקה.");

        var thread = await threads.Of(me).FirstOrDefaultAsync(t => t.Id == id);
        if (thread is null) return ServiceError.NotFound();

        // המורה כבר קיבלה התראה על מה שטרם קראה — הודעה נוספת באותה שיחה לא תשלח לה עוד מייל
        var alreadyWaiting = thread.LastSenderRole != MessageAuthor.Teacher &&
                             (thread.TeacherReadAt is null || thread.TeacherReadAt < thread.LastMessageAt);

        var message = threads.Append(thread, me.Role, body);
        // תשובה מהצד השני מחזירה לתיבה גם שיחה שנסגרה
        thread.IsClosed = false;
        thread.CounterpartReadAt = thread.LastMessageAt;

        if (alreadyWaiting) await db.SaveChangesAsync();
        else await NotifyTeacherAsync(me, thread.StudentId, thread.Subject, body);

        return MessageThreads.ToDto(message);
    }

    // ----- עזר -----

    /// <summary>תלמידה פונה בעניין עצמה; הורה — בעניין ילדיו בלבד. ההערה חייבת להיות של אותה תלמידה.</summary>
    private async Task<ServiceError?> ValidateStartAsync(Counterpart me, Guid studentId, Guid? relatedNoteId)
    {
        if (!await MayActForStudentAsync(me, studentId))
            return ServiceError.NotFound("תלמידה לא נמצאה.");

        // ההערה חייבת להיות של אותה תלמידה, ומכאן שגם של אותה מורה
        if (relatedNoteId is Guid noteId &&
            !await db.Notes.AnyAsync(n => n.Id == noteId && n.StudentId == studentId))
            return ServiceError.Invalid("ההערה המקושרת אינה של התלמידה הזו.");

        return null;
    }

    private Task<bool> MayActForStudentAsync(Counterpart me, Guid studentId) =>
        me.Role == MessageAuthor.Student
            ? Task.FromResult(me.Id == studentId)
            : family.IsChildOfAsync(me.Id, studentId);

    /// <summary>שומר את ההודעה יחד עם ההתראה למורה, ורק אחר כך שולח לה מייל.</summary>
    private async Task NotifyTeacherAsync(Counterpart me, Guid studentId, string subject, string body)
    {
        var studentName = await threads.StudentNameAsync(studentId);
        var text = MessageNotifier.NotificationText(me.Name, studentName, body);
        notifier.AddTeacherNotification(subject, text);
        await db.SaveChangesAsync();

        await notifier.EmailTeacherAsync(me.Name, subject, text);
    }
}
