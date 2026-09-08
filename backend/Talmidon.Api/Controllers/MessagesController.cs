using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Talmidon.Api.Contracts;
using Talmidon.Domain.Entities;
using Talmidon.Domain.Enums;
using Talmidon.Infrastructure.Auth;
using Talmidon.Infrastructure.Data;
using Talmidon.Infrastructure.Email;
using Talmidon.Infrastructure.Identity;
using Talmidon.Infrastructure.Multitenancy;

namespace Talmidon.Api.Controllers;

/// <summary>
/// שיחות בין המורה לתלמידות ולהורים.
///
/// מנגנון אחד לשלושה צרכים שנראו נפרדים: פנייה של תלמידה למורה, תגובה של תלמידה על הערה
/// שקיבלה, והודעה שהמורה יוזמת. כולם אותה שיחה, ולכן למורה יש תיבה אחת — ולא שלושה
/// מקומות שצריך לזכור לבדוק.
///
/// הצד השני (<see cref="MessageAuthor"/>) הוא תלמידה או הורה, ואותם נתיבים משרתים את
/// שניהם: מי שמחובר קובע מי הוא, ובקשה לשיחה שאינה שלו מקבלת 404.
/// </summary>
[ApiController]
[Route("api/messages")]
public class MessagesController(
    TalmidonDbContext db,
    ICurrentTenant currentTenant,
    UserManager<ApplicationUser> userManager,
    IEmailSender emailSender,
    ILogger<MessagesController> logger) : ControllerBase
{
    private const int PreviewLength = 200;

    private Guid TenantId => currentTenant.TenantId
        ?? throw new InvalidOperationException("No tenant in the current context.");

    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new InvalidOperationException("No user id in the current context.");

    // ===== מורה =====

    /// <summary>התיבה של המורה, מהשיחה שזזה אחרונה. שיחות סגורות יורדות לסוף.</summary>
    [Authorize(Roles = Roles.Teacher)]
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ThreadSummaryDto>>> Inbox(
        [FromQuery] Guid? studentId, [FromQuery] bool includeClosed = true)
    {
        var query = db.MessageThreads.AsQueryable();
        if (studentId is not null) query = query.Where(t => t.StudentId == studentId);
        if (!includeClosed) query = query.Where(t => !t.IsClosed);

        var threads = await query
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

        var names = await CounterpartNamesAsync(threads.Select(t => t.Thread));

        return Ok(threads.Select(t => new ThreadSummaryDto(
            t.Thread.Id, t.Thread.StudentId, t.StudentName,
            t.Thread.CounterpartRole, NameOf(t.Thread, t.StudentName, names),
            t.Thread.Subject, t.Thread.LastMessagePreview, t.Thread.LastSenderRole,
            t.Thread.LastMessageAt, t.HasUnread, t.Thread.IsClosed)));
    }

    /// <summary>מונה לתג שליד תיבת הדואר בסרגל העליון.</summary>
    [Authorize(Roles = Roles.Teacher)]
    [HttpGet("unread-count")]
    public async Task<ActionResult<UnreadCountDto>> TeacherUnreadCount()
    {
        var count = await db.MessageThreads.CountAsync(t =>
            t.LastSenderRole != MessageAuthor.Teacher &&
            (t.TeacherReadAt == null || t.TeacherReadAt < t.LastMessageAt));
        return Ok(new UnreadCountDto(count));
    }

    /// <summary>
    /// למי אפשר לכתוב: כל תלמידה שיש לה חשבון, וכל הורה מקושר. רשימה שטוחה, כדי שמסך
    /// הכתיבה יסתפק בבחירה אחת במקום בשתיים.
    /// </summary>
    [Authorize(Roles = Roles.Teacher)]
    [HttpGet("recipients")]
    public async Task<ActionResult<IEnumerable<MessageRecipientDto>>> Recipients()
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

        return Ok(recipients);
    }

    [Authorize(Roles = Roles.Teacher)]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ThreadDetailDto>> GetThread(Guid id)
    {
        var thread = await LoadThreadAsync(id);
        if (thread is null) return NotFound();

        thread.TeacherReadAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();

        return Ok(await ToDetailAsync(thread));
    }

    /// <summary>המורה פותחת שיחה מיוזמתה.</summary>
    [Authorize(Roles = Roles.Teacher)]
    [HttpPost]
    public async Task<ActionResult<ThreadDetailDto>> StartThread(TeacherStartThreadRequest request)
    {
        var body = request.Body?.Trim();
        var subject = request.Subject?.Trim();
        if (string.IsNullOrEmpty(body) || string.IsNullOrEmpty(subject))
            return BadRequest(new { message = "נושא ותוכן הם שדות חובה." });

        var student = await db.Students.FirstOrDefaultAsync(s => s.Id == request.StudentId);
        if (student is null) return NotFound(new { message = "תלמידה לא נמצאה." });

        var to = await ResolveCounterpartAsync(student, request.CounterpartRole, request.CounterpartId);
        if (to is null)
            return BadRequest(new { message = "אין למי לשלוח: לתלמידה אין חשבון ואין הורה מקושר." });

        var thread = NewThread(student.Id, to.Value.Role, to.Value.Id, subject, null);
        AppendMessage(thread, MessageAuthor.Teacher, body);
        // המורה כתבה — מבחינתה השיחה נקראה
        thread.TeacherReadAt = thread.LastMessageAt;
        db.MessageThreads.Add(thread);
        await db.SaveChangesAsync();

        await NotifyCounterpartAsync(thread, student.FullName, subject, body);

        return CreatedAtAction(nameof(GetThread), new { id = thread.Id }, await ToDetailAsync(thread));
    }

    [Authorize(Roles = Roles.Teacher)]
    [HttpPost("{id:guid}/reply")]
    public async Task<ActionResult<MessageDto>> TeacherReply(Guid id, PostMessageRequest request)
    {
        var body = request.Body?.Trim();
        if (string.IsNullOrEmpty(body)) return BadRequest(new { message = "אי אפשר לשלוח הודעה ריקה." });

        var thread = await LoadThreadAsync(id);
        if (thread is null) return NotFound();

        var message = AppendMessage(thread, MessageAuthor.Teacher, body);
        // תשובה של המורה פותחת מחדש שיחה שנסגרה, ומסמנת אותה כנקראה מצידה
        thread.IsClosed = false;
        thread.TeacherReadAt = thread.LastMessageAt;
        await db.SaveChangesAsync();

        var studentName = await db.Students.Where(s => s.Id == thread.StudentId)
            .Select(s => s.FullName).FirstAsync();
        await NotifyCounterpartAsync(thread, studentName, thread.Subject, body);

        return Ok(ToDto(message));
    }

    /// <summary>סגירת שיחה שטופלה. היא נשארת לקריאה, והודעה חדשה תפתח אותה מחדש.</summary>
    [Authorize(Roles = Roles.Teacher)]
    [HttpPost("{id:guid}/close")]
    public Task<IActionResult> Close(Guid id) => SetClosedAsync(id, true);

    [Authorize(Roles = Roles.Teacher)]
    [HttpPost("{id:guid}/reopen")]
    public Task<IActionResult> Reopen(Guid id) => SetClosedAsync(id, false);

    private async Task<IActionResult> SetClosedAsync(Guid id, bool closed)
    {
        var thread = await db.MessageThreads.FirstOrDefaultAsync(t => t.Id == id);
        if (thread is null) return NotFound();

        thread.IsClosed = closed;
        await db.SaveChangesAsync();
        return NoContent();
    }

    // ===== תלמידה והורה =====

    [Authorize(Roles = $"{Roles.Student},{Roles.Parent}")]
    [HttpGet("mine")]
    public async Task<ActionResult<IEnumerable<ThreadSummaryDto>>> MyThreads()
    {
        var me = await CurrentCounterpartAsync();
        if (me is null) return Forbid();

        var threads = await MyThreadsQuery(me.Value)
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

        return Ok(threads.Select(t => new ThreadSummaryDto(
            t.Thread.Id, t.Thread.StudentId, t.StudentName,
            t.Thread.CounterpartRole, me.Value.Name,
            t.Thread.Subject, t.Thread.LastMessagePreview, t.Thread.LastSenderRole,
            t.Thread.LastMessageAt, t.HasUnread, t.Thread.IsClosed)));
    }

    [Authorize(Roles = $"{Roles.Student},{Roles.Parent}")]
    [HttpGet("mine/unread-count")]
    public async Task<ActionResult<UnreadCountDto>> MyUnreadCount()
    {
        var me = await CurrentCounterpartAsync();
        if (me is null) return Forbid();

        var count = await MyThreadsQuery(me.Value).CountAsync(t =>
            t.LastSenderRole == MessageAuthor.Teacher &&
            (t.CounterpartReadAt == null || t.CounterpartReadAt < t.LastMessageAt));
        return Ok(new UnreadCountDto(count));
    }

    [Authorize(Roles = $"{Roles.Student},{Roles.Parent}")]
    [HttpGet("mine/{id:guid}")]
    public async Task<ActionResult<ThreadDetailDto>> MyThread(Guid id)
    {
        var me = await CurrentCounterpartAsync();
        if (me is null) return Forbid();

        var thread = await MyThreadsQuery(me.Value)
            .Include(t => t.Messages)
            .FirstOrDefaultAsync(t => t.Id == id);
        if (thread is null) return NotFound();

        thread.CounterpartReadAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();

        return Ok(await ToDetailAsync(thread));
    }

    /// <summary>
    /// פנייה חדשה למורה. תלמידה פונה בעניין עצמה ואינה שולחת מזהה; הורה חייב לציין באיזה
    /// ילד מדובר. אפשר לקשור את הפנייה להערה שהתקבלה — כך המורה רואה על מה מגיבים.
    /// </summary>
    [Authorize(Roles = $"{Roles.Student},{Roles.Parent}")]
    [HttpPost("mine")]
    public async Task<ActionResult<ThreadDetailDto>> StartMyThread(StartThreadRequest request)
    {
        var me = await CurrentCounterpartAsync();
        if (me is null) return Forbid();

        var body = request.Body?.Trim();
        var subject = request.Subject?.Trim();
        if (string.IsNullOrEmpty(body) || string.IsNullOrEmpty(subject))
            return BadRequest(new { message = "נושא ותוכן הם שדות חובה." });

        var studentId = me.Value.Role == MessageAuthor.Student ? me.Value.Id : request.StudentId;
        if (studentId is null) return BadRequest(new { message = "יש לבחור עבור מי הפנייה." });

        if (!await MayActForStudentAsync(me.Value, studentId.Value))
            return NotFound(new { message = "תלמידה לא נמצאה." });

        // ההערה חייבת להיות של אותה תלמידה, ומכאן שגם של אותה מורה
        if (request.RelatedNoteId is Guid noteId &&
            !await db.Notes.AnyAsync(n => n.Id == noteId && n.StudentId == studentId))
            return BadRequest(new { message = "ההערה המקושרת אינה של התלמידה הזו." });

        var thread = NewThread(studentId.Value, me.Value.Role, me.Value.Id, subject, request.RelatedNoteId);
        AppendMessage(thread, me.Value.Role, body);
        thread.CounterpartReadAt = thread.LastMessageAt;
        db.MessageThreads.Add(thread);

        var studentName = await db.Students.Where(s => s.Id == studentId).Select(s => s.FullName).FirstAsync();
        AddTeacherNotification(subject, MessageNotificationText(me.Value.Name, studentName, body));
        await db.SaveChangesAsync();

        await NotifyTeacherAsync($"הודעה חדשה: {subject}", MessageNotificationText(me.Value.Name, studentName, body));

        return CreatedAtAction(nameof(MyThread), new { id = thread.Id }, await ToDetailAsync(thread));
    }

    [Authorize(Roles = $"{Roles.Student},{Roles.Parent}")]
    [HttpPost("mine/{id:guid}/reply")]
    public async Task<ActionResult<MessageDto>> MyReply(Guid id, PostMessageRequest request)
    {
        var me = await CurrentCounterpartAsync();
        if (me is null) return Forbid();

        var body = request.Body?.Trim();
        if (string.IsNullOrEmpty(body)) return BadRequest(new { message = "אי אפשר לשלוח הודעה ריקה." });

        var thread = await MyThreadsQuery(me.Value).FirstOrDefaultAsync(t => t.Id == id);
        if (thread is null) return NotFound();

        // המורה כבר קיבלה התראה על מה שטרם קראה — הודעה נוספת באותה שיחה לא תשלח לה עוד מייל
        var alreadyWaiting = thread.LastSenderRole != MessageAuthor.Teacher &&
                             (thread.TeacherReadAt is null || thread.TeacherReadAt < thread.LastMessageAt);

        var message = AppendMessage(thread, me.Value.Role, body);
        // תשובה מהצד השני מחזירה לתיבה גם שיחה שנסגרה
        thread.IsClosed = false;
        thread.CounterpartReadAt = thread.LastMessageAt;

        var studentName = await db.Students.Where(s => s.Id == thread.StudentId).Select(s => s.FullName).FirstAsync();
        var text = MessageNotificationText(me.Value.Name, studentName, body);
        if (!alreadyWaiting) AddTeacherNotification(thread.Subject, text);
        await db.SaveChangesAsync();

        if (!alreadyWaiting) await NotifyTeacherAsync($"הודעה חדשה: {thread.Subject}", text);

        return Ok(ToDto(message));
    }

    // ----- עזר -----

    private Task<Parent?> CurrentParentAsync() => db.Parents.FirstOrDefaultAsync(p => p.UserId == CurrentUserId);
    private Task<Student?> CurrentStudentAsync() => db.Students.FirstOrDefaultAsync(s => s.UserId == CurrentUserId);

    private readonly record struct Counterpart(MessageAuthor Role, Guid Id, string Name);

    /// <summary>מי מדבר מול המורה כרגע — לפי התפקיד שבטוקן, לא לפי מה שנשלח בבקשה.</summary>
    private async Task<Counterpart?> CurrentCounterpartAsync()
    {
        if (User.IsInRole(Roles.Student))
        {
            var student = await CurrentStudentAsync();
            return student is null ? null : new Counterpart(MessageAuthor.Student, student.Id, student.FullName);
        }

        if (User.IsInRole(Roles.Parent))
        {
            var parent = await CurrentParentAsync();
            return parent is null ? null : new Counterpart(MessageAuthor.Parent, parent.Id, parent.FullName);
        }

        return null;
    }

    /// <summary>השיחות של הצד השני בלבד — הבסיס לכל נתיב שאינו של המורה.</summary>
    private IQueryable<MessageThread> MyThreadsQuery(Counterpart me) =>
        db.MessageThreads.Where(t => t.CounterpartRole == me.Role && t.CounterpartId == me.Id);

    /// <summary>תלמידה פונה בעניין עצמה; הורה — בעניין ילדיו בלבד.</summary>
    private Task<bool> MayActForStudentAsync(Counterpart me, Guid studentId) =>
        me.Role == MessageAuthor.Student
            ? Task.FromResult(me.Id == studentId)
            : db.StudentParents.AnyAsync(sp => sp.ParentId == me.Id && sp.StudentId == studentId);

    /// <summary>
    /// למי המורה שולחת. ברירת מחדל נוחה: התלמידה עצמה אם יש לה חשבון, אחרת ההורה הראשון
    /// המקושר — כדי שלא תיתקע מול טופס שדורש בחירה שאין בה למעשה ברירה.
    /// </summary>
    private async Task<Counterpart?> ResolveCounterpartAsync(
        Student student, MessageAuthor? role, Guid? counterpartId)
    {
        if (role == MessageAuthor.Student)
        {
            if (student.UserId is null) return null;
            return new Counterpart(MessageAuthor.Student, student.Id, student.FullName);
        }

        if (role == MessageAuthor.Parent)
        {
            if (counterpartId is not Guid parentId) return null;
            var parent = await db.Parents.FirstOrDefaultAsync(p => p.Id == parentId);
            if (parent is null) return null;

            var linked = await db.StudentParents.AnyAsync(sp => sp.StudentId == student.Id && sp.ParentId == parentId);
            return linked ? new Counterpart(MessageAuthor.Parent, parent.Id, parent.FullName) : null;
        }

        if (student.UserId is not null)
            return new Counterpart(MessageAuthor.Student, student.Id, student.FullName);

        var firstParent = await db.StudentParents
            .Where(sp => sp.StudentId == student.Id)
            .OrderBy(sp => sp.Parent.FullName)
            .Select(sp => new { sp.ParentId, sp.Parent.FullName })
            .FirstOrDefaultAsync();

        return firstParent is null ? null : new Counterpart(MessageAuthor.Parent, firstParent.ParentId, firstParent.FullName);
    }

    private MessageThread NewThread(
        Guid studentId, MessageAuthor counterpartRole, Guid counterpartId, string subject, Guid? relatedNoteId) =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            StudentId = studentId,
            CounterpartRole = counterpartRole,
            CounterpartId = counterpartId,
            Subject = subject,
            RelatedNoteId = relatedNoteId,
            CreatedAt = DateTimeOffset.UtcNow
        };

    /// <summary>מוסיף הודעה ומעדכן את שדות הסיכום של השיחה — שני הדברים תמיד יחד.</summary>
    private Message AppendMessage(MessageThread thread, MessageAuthor sender, string body)
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
        db.Messages.Add(message);
        thread.Messages.Add(message);

        thread.LastMessageAt = message.CreatedAt;
        thread.LastSenderRole = sender;
        thread.LastMessagePreview = body.Length <= PreviewLength ? body : body[..PreviewLength];
        return message;
    }

    private Task<MessageThread?> LoadThreadAsync(Guid id) =>
        db.MessageThreads.Include(t => t.Messages).FirstOrDefaultAsync(t => t.Id == id);

    private async Task<ThreadDetailDto> ToDetailAsync(MessageThread thread)
    {
        var studentName = await db.Students.Where(s => s.Id == thread.StudentId)
            .Select(s => s.FullName).FirstAsync();
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

    private static MessageDto ToDto(Message m) => new(m.Id, m.SenderRole, m.Body, m.CreatedAt);

    /// <summary>
    /// שמות ההורים שבצד השני של השיחות. שאילתה אחת לכולם: השם אינו נשמר על השיחה כדי
    /// שלא ייתקע על ערך ישן אחרי שינוי שם.
    /// </summary>
    private async Task<Dictionary<Guid, string>> CounterpartNamesAsync(IEnumerable<MessageThread> threads)
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

    private static string NameOf(MessageThread thread, string studentName, IReadOnlyDictionary<Guid, string> parentNames) =>
        thread.CounterpartRole == MessageAuthor.Student
            ? studentName
            : parentNames.TryGetValue(thread.CounterpartId, out var name) ? name : "הורה";

    private static string Truncate(string text, int max) =>
        text.Length <= max ? text : text[..(max - 1)] + "…";

    private static string MessageNotificationText(string senderName, string studentName, string body)
    {
        var who = senderName == studentName ? senderName : $"{senderName} (בעניין {studentName})";
        var preview = Truncate(body, PreviewLength);
        return $"{who}: {preview}";
    }

    private void AddTeacherNotification(string subject, string message)
    {
        db.Notifications.Add(new Notification
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            Type = NotificationType.Message,
            // כותרת ההתראה מוגבלת ל-200 תווים במסד, ונושא השיחה לבדו כבר יכול למלא אותם
            Title = Truncate($"הודעה חדשה: {subject}", 200),
            Message = message,
            LinkPath = "/app/messages",
            IsRead = false,
            CreatedAt = DateTimeOffset.UtcNow
        });
    }

    private static string BuildEmailHtml(string title, string message) =>
        $"""
        <div dir="rtl" style="font-family:Arial,sans-serif">
          <h2>{WebUtility.HtmlEncode(title)}</h2>
          <p style="white-space:pre-wrap">{WebUtility.HtmlEncode(message)}</p>
        </div>
        """;

    private async Task NotifyTeacherAsync(string subject, string message)
    {
        var teacher = await db.Teachers.FirstOrDefaultAsync(t => t.Id == TenantId);
        if (teacher is null) return;

        var user = await userManager.FindByIdAsync(teacher.UserId);
        if (user?.Email is null) return;

        try { await emailSender.SendAsync(user.Email, subject, BuildEmailHtml(subject, message)); }
        catch (Exception ex) { logger.LogError(ex, "Failed to send teacher message email."); }
    }

    /// <summary>שולח מייל לצד השני כשהמורה כותבת. לתלמידה בלי חשבון-מייל פשוט אין למי לשלוח.</summary>
    private async Task NotifyCounterpartAsync(MessageThread thread, string studentName, string subject, string body)
    {
        string? email = null;

        if (thread.CounterpartRole == MessageAuthor.Parent)
        {
            email = await db.Parents.Where(p => p.Id == thread.CounterpartId)
                .Select(p => (string?)p.Email).FirstOrDefaultAsync();
        }
        else
        {
            var userId = await db.Students.Where(s => s.Id == thread.CounterpartId)
                .Select(s => s.UserId).FirstOrDefaultAsync();
            if (userId is not null) email = (await userManager.FindByIdAsync(userId))?.Email;
        }

        if (string.IsNullOrWhiteSpace(email)) return;

        var title = $"הודעה מהמורה: {subject}";
        var text = $"בעניין {studentName}\n\n{body}";
        try { await emailSender.SendAsync(email, title, BuildEmailHtml(title, text)); }
        catch (Exception ex) { logger.LogError(ex, "Failed to send message email to the counterpart."); }
    }
}
