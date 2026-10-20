using Microsoft.EntityFrameworkCore;
using Talmidon.Api.Contracts;
using Talmidon.Domain.Enums;
using Talmidon.Infrastructure.Data;

namespace Talmidon.Api.Services.Messages;

/// <summary>רשימות השיחות ומוני ה"לא נקרא" — למורה ולצד השני. קריאה בלבד.</summary>
public class MessageQueries(TalmidonDbContext db, MessageThreads threads)
{
    /// <summary>התיבה של המורה, מהשיחה שזזה אחרונה. שיחות סגורות יורדות לסוף.</summary>
    public async Task<List<ThreadSummaryDto>> InboxAsync(Guid? studentId, bool includeClosed)
    {
        var query = db.MessageThreads.AsQueryable();
        if (studentId is not null) query = query.Where(t => t.StudentId == studentId);
        if (!includeClosed) query = query.Where(t => !t.IsClosed);

        var rows = await query
            .OrderBy(t => t.IsClosed)
            .ThenByDescending(t => t.LastMessageAt)
            .Select(t => new
            {
                Thread = t,
                StudentName = t.Student.FullName,
                HasUnread = t.LastSenderRole != MessageAuthor.Teacher &&
                            (t.TeacherReadAt == null || t.TeacherReadAt < t.LastMessageAt)
            })
            .ToListAsync();

        var names = await threads.CounterpartNamesAsync(rows.Select(r => r.Thread));

        return rows.Select(r => new ThreadSummaryDto(
            r.Thread.Id, r.Thread.StudentId, r.StudentName,
            r.Thread.CounterpartRole, MessageThreads.NameOf(r.Thread, r.StudentName, names),
            r.Thread.Subject, r.Thread.LastMessagePreview, r.Thread.LastSenderRole,
            r.Thread.LastMessageAt, r.HasUnread, r.Thread.IsClosed)).ToList();
    }

    public Task<int> TeacherUnreadCountAsync() =>
        db.MessageThreads.CountAsync(t =>
            t.LastSenderRole != MessageAuthor.Teacher &&
            (t.TeacherReadAt == null || t.TeacherReadAt < t.LastMessageAt));

    /// <summary>
    /// למי אפשר לכתוב: כל תלמידה שיש לה חשבון, וכל הורה מקושר. רשימה שטוחה, כדי שמסך
    /// הכתיבה יסתפק בבחירה אחת במקום בשתיים.
    /// </summary>
    public async Task<List<MessageRecipientDto>> RecipientsAsync()
    {
        var students = await db.Students
            .OrderBy(s => s.FullName)
            .Select(s => new { s.Id, s.FullName, HasLogin = s.UserId != null })
            .ToListAsync();

        var links = await db.StudentParents
            .Select(sp => new { sp.StudentId, sp.ParentId, sp.Parent.FullName })
            .ToListAsync();

        var recipients = new List<MessageRecipientDto>();
        foreach (var student in students)
        {
            if (student.HasLogin)
                recipients.Add(new MessageRecipientDto(
                    student.Id, student.FullName, MessageAuthor.Student, student.Id, student.FullName));

            foreach (var link in links.Where(l => l.StudentId == student.Id).OrderBy(l => l.FullName))
                recipients.Add(new MessageRecipientDto(
                    student.Id, student.FullName, MessageAuthor.Parent, link.ParentId, link.FullName));
        }

        return recipients;
    }

    public async Task<List<ThreadSummaryDto>> ThreadsOfAsync(Counterpart me)
    {
        var rows = await threads.Of(me)
            .OrderBy(t => t.IsClosed)
            .ThenByDescending(t => t.LastMessageAt)
            .Select(t => new
            {
                Thread = t,
                StudentName = t.Student.FullName,
                HasUnread = t.LastSenderRole == MessageAuthor.Teacher &&
                            (t.CounterpartReadAt == null || t.CounterpartReadAt < t.LastMessageAt)
            })
            .ToListAsync();

        return rows.Select(r => new ThreadSummaryDto(
            r.Thread.Id, r.Thread.StudentId, r.StudentName,
            r.Thread.CounterpartRole, me.Name,
            r.Thread.Subject, r.Thread.LastMessagePreview, r.Thread.LastSenderRole,
            r.Thread.LastMessageAt, r.HasUnread, r.Thread.IsClosed)).ToList();
    }

    public Task<int> UnreadCountOfAsync(Counterpart me) =>
        threads.Of(me).CountAsync(t =>
            t.LastSenderRole == MessageAuthor.Teacher &&
            (t.CounterpartReadAt == null || t.CounterpartReadAt < t.LastMessageAt));
}
