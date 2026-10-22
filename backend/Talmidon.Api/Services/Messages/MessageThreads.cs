using Microsoft.EntityFrameworkCore;
using Talmidon.Api.Contracts;
using Talmidon.Domain.Entities;
using Talmidon.Domain.Enums;
using Talmidon.Infrastructure.Data;
using Talmidon.Infrastructure.Multitenancy;

namespace Talmidon.Api.Services.Messages;

/// <summary>מי מדבר מול המורה: תלמידה או הורה.</summary>
public readonly record struct Counterpart(MessageAuthor Role, Guid Id, string Name);

/// <summary>
/// המכניקה המשותפת של שיחה, לשני הצדדים: יצירה, הוספת הודעה, זיהוי הצד השני
/// והרכבת התצוגה המלאה.
/// </summary>
public class MessageThreads(TalmidonDbContext db, ICurrentTenant currentTenant, FamilyMembers family)
{
    public const int PreviewLength = 200;

    private Guid TenantId => currentTenant.TenantId
        ?? throw new InvalidOperationException("No tenant in the current context.");

    /// <summary>מי מדבר מול המורה כרגע — לפי התפקיד שבטוקן, לא לפי מה שנשלח בבקשה.</summary>
    public async Task<Counterpart?> CurrentCounterpartAsync(string userId, MessageAuthor? role)
    {
        if (role == MessageAuthor.Student)
        {
            var student = await family.StudentAsync(userId);
            return student is null ? null : new Counterpart(MessageAuthor.Student, student.Id, student.FullName);
        }

        if (role == MessageAuthor.Parent)
        {
            var parent = await family.ParentAsync(userId);
            return parent is null ? null : new Counterpart(MessageAuthor.Parent, parent.Id, parent.FullName);
        }

        return null;
    }

    /// <summary>השיחות של הצד השני בלבד — הבסיס לכל נתיב שאינו של המורה.</summary>
    public IQueryable<MessageThread> Of(Counterpart me) =>
        db.MessageThreads.Where(t => t.CounterpartRole == me.Role && t.CounterpartId == me.Id);

    public Task<MessageThread?> LoadAsync(Guid id) =>
        db.MessageThreads.Include(t => t.Messages).FirstOrDefaultAsync(t => t.Id == id);

    public Task<string> StudentNameAsync(Guid studentId) =>
        db.Students.Where(s => s.Id == studentId).Select(s => s.FullName).FirstAsync();

    public MessageThread New(
        Guid studentId, Counterpart counterpart, string subject, Guid? relatedNoteId) =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            StudentId = studentId,
            CounterpartRole = counterpart.Role,
            CounterpartId = counterpart.Id,
            Subject = subject,
            RelatedNoteId = relatedNoteId,
            CreatedAt = DateTimeOffset.UtcNow
        };

    /// <summary>מוסיף הודעה ומעדכן את שדות הסיכום של השיחה — שני הדברים תמיד יחד.</summary>
    public Message Append(MessageThread thread, MessageAuthor sender, string body)
    {
        var message = new Message
        {
            Id = Guid.NewGuid(),
            TenantId = thread.TenantId,
            ThreadId = thread.Id,
            Thread = thread,
            SenderRole = sender,
            Body = body,
            CreatedAt = DateTimeOffset.UtcNow
        };
        // רק Add אחד: הצבת Thread גורמת ל-EF לשרשר את ההודעה גם אל thread.Messages,
        // ותוספת ידנית שם הייתה מכניסה אותה פעמיים לתשובה שחוזרת ללקוח.
        db.Messages.Add(message);

        thread.LastMessageAt = message.CreatedAt;
        thread.LastSenderRole = sender;
        thread.LastMessagePreview = body.Length <= PreviewLength ? body : body[..PreviewLength];
        return message;
    }

    public async Task<ThreadDetailDto> ToDetailAsync(MessageThread thread)
    {
        var studentName = await StudentNameAsync(thread.StudentId);
        var names = await CounterpartNamesAsync([thread]);

        var noteContent = thread.RelatedNoteId is Guid noteId
            ? await db.Notes.Where(n => n.Id == noteId).Select(n => (string?)n.Content).FirstOrDefaultAsync()
            : null;

        var messages = thread.Messages.OrderBy(m => m.CreatedAt).Select(ToDto).ToList();

        return new ThreadDetailDto(
            thread.Id, thread.StudentId, studentName,
            thread.CounterpartRole, NameOf(thread, studentName, names),
            thread.Subject, thread.RelatedNoteId, noteContent,
            thread.IsClosed, thread.CreatedAt, messages);
    }

    public static MessageDto ToDto(Message m) => new(m.Id, m.SenderRole, m.Body, m.CreatedAt);

    /// <summary>
    /// שמות ההורים שבצד השני של השיחות. שאילתה אחת לכולם: השם אינו נשמר על השיחה כדי
    /// שלא ייתקע על ערך ישן אחרי שינוי שם.
    /// </summary>
    public async Task<Dictionary<Guid, string>> CounterpartNamesAsync(IEnumerable<MessageThread> threads)
    {
        var parentIds = threads
            .Where(t => t.CounterpartRole == MessageAuthor.Parent)
            .Select(t => t.CounterpartId)
            .Distinct()
            .ToList();

        if (parentIds.Count == 0) return [];

        return await db.Parents
            .Where(p => parentIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.FullName);
    }

    public static string NameOf(MessageThread thread, string studentName, IReadOnlyDictionary<Guid, string> parentNames) =>
        thread.CounterpartRole == MessageAuthor.Student
            ? studentName
            : parentNames.TryGetValue(thread.CounterpartId, out var name) ? name : "הורה";

    /// <summary>גוף הודעה אחרי ניקוי רווחים, או null אם לא נשאר בו כלום.</summary>
    public static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
