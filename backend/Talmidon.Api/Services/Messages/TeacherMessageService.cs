using Microsoft.EntityFrameworkCore;
using Talmidon.Api.Contracts;
using Talmidon.Domain.Entities;
using Talmidon.Domain.Enums;
using Talmidon.Infrastructure.Data;

namespace Talmidon.Api.Services.Messages;

/// <summary>הצד של המורה בשיחה: פתיחה, תשובה, קריאה, סגירה ופתיחה מחדש.</summary>
public class TeacherMessageService(TalmidonDbContext db, MessageThreads threads, MessageNotifier notifier)
{
    /// <summary>פתיחת שיחה ע"י המורה מסמנת אותה כנקראה מצידה.</summary>
    public async Task<ServiceResult<ThreadDetailDto>> OpenAsync(Guid id)
    {
        var thread = await threads.LoadAsync(id);
        if (thread is null) return ServiceError.NotFound();

        thread.TeacherReadAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();

        return await threads.ToDetailAsync(thread);
    }

    /// <summary>המורה פותחת שיחה מיוזמתה.</summary>
    public async Task<ServiceResult<ThreadDetailDto>> StartAsync(TeacherStartThreadRequest request)
    {
        var body = MessageThreads.Clean(request.Body);
        var subject = MessageThreads.Clean(request.Subject);
        if (body is null || subject is null) return ServiceError.Invalid("נושא ותוכן הם שדות חובה.");

        var student = await db.Students.FirstOrDefaultAsync(s => s.Id == request.StudentId);
        if (student is null) return ServiceError.NotFound("תלמידה לא נמצאה.");

        var to = await ResolveCounterpartAsync(student, request.CounterpartRole, request.CounterpartId);
        if (to is null) return ServiceError.Invalid("אין למי לשלוח: לתלמידה אין חשבון ואין הורה מקושר.");

        var thread = threads.New(student.Id, to.Value, subject, null);
        threads.Append(thread, MessageAuthor.Teacher, body);
        // המורה כתבה — מבחינתה השיחה נקראה
        thread.TeacherReadAt = thread.LastMessageAt;
        db.MessageThreads.Add(thread);
        await db.SaveChangesAsync();

        await notifier.EmailCounterpartAsync(thread, student.FullName, subject, body);
        return await threads.ToDetailAsync(thread);
    }

    public async Task<ServiceResult<MessageDto>> ReplyAsync(Guid id, PostMessageRequest request)
    {
        var body = MessageThreads.Clean(request.Body);
        if (body is null) return ServiceError.Invalid("אי אפשר לשלוח הודעה ריקה.");

        var thread = await threads.LoadAsync(id);
        if (thread is null) return ServiceError.NotFound();

        var message = threads.Append(thread, MessageAuthor.Teacher, body);
        // תשובה של המורה פותחת מחדש שיחה שנסגרה, ומסמנת אותה כנקראה מצידה
        thread.IsClosed = false;
        thread.TeacherReadAt = thread.LastMessageAt;
        await db.SaveChangesAsync();

        var studentName = await threads.StudentNameAsync(thread.StudentId);
        await notifier.EmailCounterpartAsync(thread, studentName, thread.Subject, body);
        return MessageThreads.ToDto(message);
    }

    /// <summary>שיחה סגורה נשארת לקריאה, והודעה חדשה תפתח אותה מחדש.</summary>
    public async Task<ServiceError?> SetClosedAsync(Guid id, bool closed)
    {
        var thread = await db.MessageThreads.FirstOrDefaultAsync(t => t.Id == id);
        if (thread is null) return ServiceError.NotFound();

        thread.IsClosed = closed;
        await db.SaveChangesAsync();
        return null;
    }

    /// <summary>
    /// למי המורה שולחת. ברירת מחדל נוחה: התלמידה עצמה אם יש לה חשבון, אחרת ההורה הראשון
    /// המקושר — כדי שלא תיתקע מול טופס שדורש בחירה שאין בה למעשה ברירה.
    /// </summary>
    private async Task<Counterpart?> ResolveCounterpartAsync(Student student, MessageAuthor? role, Guid? counterpartId)
    {
        if (role == MessageAuthor.Student) return StudentCounterpart(student);
        if (role == MessageAuthor.Parent) return await LinkedParentAsync(student, counterpartId);
        return StudentCounterpart(student) ?? await FirstParentAsync(student);
    }

    private static Counterpart? StudentCounterpart(Student student) =>
        student.UserId is null ? null : new Counterpart(MessageAuthor.Student, student.Id, student.FullName);

    private async Task<Counterpart?> LinkedParentAsync(Student student, Guid? counterpartId)
    {
        if (counterpartId is not Guid parentId) return null;
        var parent = await db.Parents.FirstOrDefaultAsync(p => p.Id == parentId);
        if (parent is null) return null;

        var linked = await db.StudentParents.AnyAsync(sp => sp.StudentId == student.Id && sp.ParentId == parentId);
        return linked ? new Counterpart(MessageAuthor.Parent, parent.Id, parent.FullName) : null;
    }

    private async Task<Counterpart?> FirstParentAsync(Student student)
    {
        var firstParent = await db.StudentParents
            .Where(sp => sp.StudentId == student.Id)
            .OrderBy(sp => sp.Parent.FullName)
            .Select(sp => new { sp.ParentId, sp.Parent.FullName })
            .FirstOrDefaultAsync();

        return firstParent is null ? null : new Counterpart(MessageAuthor.Parent, firstParent.ParentId, firstParent.FullName);
    }
}
