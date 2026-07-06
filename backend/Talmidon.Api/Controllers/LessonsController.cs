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
/// יומן השיעורים: ניהול מלא למורה (T4/T5/T6), בקשות ועיון להורה (R2), עיון בלבד לתלמיד (S2).
/// כל הפעולות מוגבלות אוטומטית לדייר הנוכחי ע"י ה-Global Query Filter; מעבר לכך נאכפת כאן
/// שייכות ההורה/התלמיד לתלמיד הספציפי (הרשאה ברמת שורה).
/// </summary>
[ApiController]
[Route("api/lessons")]
public class LessonsController(
    TalmidonDbContext db,
    ICurrentTenant currentTenant,
    UserManager<ApplicationUser> userManager,
    IEmailSender emailSender,
    ILogger<LessonsController> logger) : ControllerBase
{
    private Guid TenantId => currentTenant.TenantId
        ?? throw new InvalidOperationException("No tenant in the current context.");

    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new InvalidOperationException("No user id in the current context.");

    private Task<Parent?> CurrentParentAsync() => db.Parents.FirstOrDefaultAsync(p => p.UserId == CurrentUserId);
    private Task<Student?> CurrentStudentAsync() => db.Students.FirstOrDefaultAsync(s => s.UserId == CurrentUserId);

    // ===== מורה: יומן =====

    [Authorize(Roles = Roles.Teacher)]
    [HttpGet]
    public async Task<ActionResult<IEnumerable<LessonDto>>> List(
        [FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to,
        [FromQuery] Guid? studentId, [FromQuery] LessonStatus? status)
    {
        var query = db.Lessons.AsQueryable();
        if (from is not null) query = query.Where(l => l.EndTime >= from);
        if (to is not null) query = query.Where(l => l.StartTime <= to);
        if (studentId is not null) query = query.Where(l => l.StudentId == studentId);
        if (status is not null) query = query.Where(l => l.Status == status);

        var lessons = await query
            .OrderBy(l => l.StartTime)
            .Select(l => new LessonDto(
                l.Id, l.StudentId, l.Student.FullName, l.StartTime, l.EndTime, l.Status, l.Origin,
                l.Homework, l.PaymentRequired, l.Amount, l.PaymentId != null, l.CompletedAt))
            .ToListAsync();
        return Ok(lessons);
    }

    [Authorize(Roles = Roles.Teacher)]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<LessonDto>> GetById(Guid id)
    {
        var lesson = await db.Lessons
            .Where(l => l.Id == id)
            .Select(l => new LessonDto(
                l.Id, l.StudentId, l.Student.FullName, l.StartTime, l.EndTime, l.Status, l.Origin,
                l.Homework, l.PaymentRequired, l.Amount, l.PaymentId != null, l.CompletedAt))
            .FirstOrDefaultAsync();
        return lesson is null ? NotFound() : Ok(lesson);
    }

    [Authorize(Roles = Roles.Teacher)]
    [HttpPost]
    public async Task<ActionResult<LessonDto>> Create(CreateLessonRequest request)
    {
        if (request.EndTime <= request.StartTime)
            return BadRequest(new { message = "שעת הסיום חייבת להיות אחרי שעת ההתחלה." });

        var student = await db.Students.FirstOrDefaultAsync(s => s.Id == request.StudentId);
        if (student is null) return NotFound(new { message = "תלמיד לא נמצא." });

        var lesson = new Lesson
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            StudentId = student.Id,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            Status = LessonStatus.Scheduled,
            Origin = LessonOrigin.Teacher
        };
        db.Lessons.Add(lesson);
        await db.SaveChangesAsync();

        await NotifyParentsAsync(student.Id, "הוספת שיעור",
            $"נקבע שיעור חדש עבור {student.FullName} בתאריך {FormatDate(lesson.StartTime)}.");

        return CreatedAtAction(nameof(GetById), new { id = lesson.Id }, ToDto(lesson, student.FullName));
    }

    [Authorize(Roles = Roles.Teacher)]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateLessonRequest request)
    {
        if (request.EndTime <= request.StartTime)
            return BadRequest(new { message = "שעת הסיום חייבת להיות אחרי שעת ההתחלה." });

        var lesson = await db.Lessons.Include(l => l.Student).FirstOrDefaultAsync(l => l.Id == id);
        if (lesson is null) return NotFound();
        if (lesson.Status != LessonStatus.Scheduled)
            return Conflict(new { message = "ניתן לעדכן מועד רק לשיעור מתוזמן." });

        lesson.StartTime = request.StartTime;
        lesson.EndTime = request.EndTime;
        await db.SaveChangesAsync();

        await NotifyParentsAsync(lesson.StudentId, "עדכון שיעור",
            $"מועד השיעור של {lesson.Student.FullName} עודכן ל-{FormatDate(lesson.StartTime)}.");

        return NoContent();
    }

    [Authorize(Roles = Roles.Teacher)]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var lesson = await db.Lessons.Include(l => l.Student).FirstOrDefaultAsync(l => l.Id == id);
        if (lesson is null) return NotFound();
        if (lesson.Status == LessonStatus.Completed)
            return Conflict(new { message = "לא ניתן למחוק שיעור שהושלם." });

        var (studentId, studentName, startTime) = (lesson.StudentId, lesson.Student.FullName, lesson.StartTime);

        db.Lessons.Remove(lesson);
        await db.SaveChangesAsync();

        await NotifyParentsAsync(studentId, "ביטול שיעור",
            $"השיעור של {studentName} בתאריך {FormatDate(startTime)} בוטל.");

        return NoContent();
    }

    /// <summary>T5 — סיום שיעור: סטטוס, תשלום, שיעורי בית, והערה פדגוגית אופציונלית.</summary>
    [Authorize(Roles = Roles.Teacher)]
    [HttpPost("{id:guid}/complete")]
    public async Task<ActionResult<LessonDto>> Complete(Guid id, CompleteLessonRequest request)
    {
        if (request.Completed && request.PaymentRequired && request.Amount <= 0)
            return BadRequest(new { message = "יש להזין סכום חיוב." });

        var lesson = await db.Lessons.Include(l => l.Student).FirstOrDefaultAsync(l => l.Id == id);
        if (lesson is null) return NotFound();
        if (lesson.Status != LessonStatus.Scheduled)
            return Conflict(new { message = "ניתן לסמן סיום רק לשיעור מתוזמן." });

        if (request.Completed)
        {
            lesson.Status = LessonStatus.Completed;
            lesson.PaymentRequired = request.PaymentRequired;
            lesson.Amount = request.PaymentRequired ? request.Amount : 0;
            lesson.Homework = request.Homework;
            lesson.CompletedAt = DateTimeOffset.UtcNow;
        }
        else
        {
            lesson.Status = LessonStatus.Cancelled;
            lesson.PaymentRequired = false;
            lesson.Amount = 0;
        }

        if (!string.IsNullOrWhiteSpace(request.NoteContent))
        {
            // ברירת מחדל שנאכפת בשרת: הערה גלויה לתלמיד תמיד גלויה גם להורה
            db.Notes.Add(new Note
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                StudentId = lesson.StudentId,
                LessonId = lesson.Id,
                Content = request.NoteContent,
                VisibleToStudent = request.NoteVisibleToStudent,
                VisibleToParent = request.NoteVisibleToParent || request.NoteVisibleToStudent,
                CreatedAt = DateTimeOffset.UtcNow
            });
        }

        await db.SaveChangesAsync();

        if (!request.Completed)
        {
            await NotifyParentsAsync(lesson.StudentId, "ביטול שיעור",
                $"השיעור של {lesson.Student.FullName} בתאריך {FormatDate(lesson.StartTime)} בוטל.");
        }

        return Ok(ToDto(lesson, lesson.Student.FullName));
    }

    // ===== מורה: בקשות שיעור חדש ממתינות (Origin=Parent, Status=Requested) =====

    [Authorize(Roles = Roles.Teacher)]
    [HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> ApproveRequest(Guid id)
    {
        var lesson = await db.Lessons.FirstOrDefaultAsync(l => l.Id == id);
        if (lesson is null) return NotFound();
        if (lesson.Status != LessonStatus.Requested)
            return Conflict(new { message = "ניתן לאשר רק בקשת שיעור ממתינה." });

        lesson.Status = LessonStatus.Scheduled;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [Authorize(Roles = Roles.Teacher)]
    [HttpPost("{id:guid}/decline")]
    public async Task<IActionResult> DeclineRequest(Guid id)
    {
        var lesson = await db.Lessons.FirstOrDefaultAsync(l => l.Id == id);
        if (lesson is null) return NotFound();
        if (lesson.Status != LessonStatus.Requested)
            return Conflict(new { message = "ניתן לדחות רק בקשת שיעור ממתינה." });

        lesson.Status = LessonStatus.Declined;
        await db.SaveChangesAsync();
        return NoContent();
    }

    // ===== מורה: בקשות שינוי/ביטול לשיעור קיים (T6) =====

    [Authorize(Roles = Roles.Teacher)]
    [HttpGet("change-requests")]
    public async Task<ActionResult<IEnumerable<ChangeRequestDto>>> ListChangeRequests([FromQuery] ChangeRequestStatus? status)
    {
        var query = status is null
            ? db.LessonChangeRequests.Where(c => c.Status == ChangeRequestStatus.Pending)
            : db.LessonChangeRequests.Where(c => c.Status == status);

        var items = await query
            .OrderBy(c => c.CreatedAt)
            .Select(c => new ChangeRequestDto(
                c.Id, c.LessonId, c.Lesson.StudentId, c.Lesson.Student.FullName, c.RequestedByParent.FullName,
                c.Type, c.Lesson.StartTime, c.Lesson.EndTime, c.ProposedStartTime, c.ProposedEndTime,
                c.Reason, c.Status, c.CreatedAt))
            .ToListAsync();
        return Ok(items);
    }

    [Authorize(Roles = Roles.Teacher)]
    [HttpPost("change-requests/{id:guid}/approve")]
    public async Task<IActionResult> ApproveChangeRequest(Guid id)
    {
        var request = await db.LessonChangeRequests.Include(c => c.Lesson).FirstOrDefaultAsync(c => c.Id == id);
        if (request is null) return NotFound();
        if (request.Status != ChangeRequestStatus.Pending)
            return Conflict(new { message = "הבקשה כבר טופלה." });

        if (request.Type == ChangeRequestType.Cancel)
        {
            request.Lesson.Status = LessonStatus.Cancelled;
        }
        else
        {
            request.Lesson.StartTime = request.ProposedStartTime!.Value;
            request.Lesson.EndTime = request.ProposedEndTime!.Value;
        }

        request.Status = ChangeRequestStatus.Approved;
        request.ResolvedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [Authorize(Roles = Roles.Teacher)]
    [HttpPost("change-requests/{id:guid}/reject")]
    public async Task<IActionResult> RejectChangeRequest(Guid id)
    {
        var request = await db.LessonChangeRequests.FirstOrDefaultAsync(c => c.Id == id);
        if (request is null) return NotFound();
        if (request.Status != ChangeRequestStatus.Pending)
            return Conflict(new { message = "הבקשה כבר טופלה." });

        request.Status = ChangeRequestStatus.Rejected;
        request.ResolvedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        return NoContent();
    }

    // ===== הורה (R2) =====

    [Authorize(Roles = Roles.Parent)]
    [HttpGet("mine")]
    public async Task<ActionResult<IEnumerable<LessonDto>>> MyChildrensLessons([FromQuery] Guid? studentId)
    {
        var parent = await CurrentParentAsync();
        if (parent is null) return Forbid();

        var childIds = db.StudentParents.Where(sp => sp.ParentId == parent.Id).Select(sp => sp.StudentId);
        var query = db.Lessons.Where(l => childIds.Contains(l.StudentId));
        if (studentId is not null) query = query.Where(l => l.StudentId == studentId);

        var lessons = await query
            .OrderBy(l => l.StartTime)
            .Select(l => new LessonDto(
                l.Id, l.StudentId, l.Student.FullName, l.StartTime, l.EndTime, l.Status, l.Origin,
                l.Homework, l.PaymentRequired, l.Amount, l.PaymentId != null, l.CompletedAt))
            .ToListAsync();
        return Ok(lessons);
    }

    /// <summary>בקשת שיעור חדש (נכנס כ-Requested עד אישור המורה).</summary>
    [Authorize(Roles = Roles.Parent)]
    [HttpPost("requests")]
    public async Task<ActionResult<LessonDto>> RequestLesson(CreateLessonRequest request)
    {
        if (request.EndTime <= request.StartTime)
            return BadRequest(new { message = "שעת הסיום חייבת להיות אחרי שעת ההתחלה." });

        var parent = await CurrentParentAsync();
        if (parent is null) return Forbid();

        var isOwnChild = await db.StudentParents.AnyAsync(sp => sp.ParentId == parent.Id && sp.StudentId == request.StudentId);
        if (!isOwnChild) return Forbid();

        var student = await db.Students.FirstAsync(s => s.Id == request.StudentId);

        var lesson = new Lesson
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            StudentId = student.Id,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            Status = LessonStatus.Requested,
            Origin = LessonOrigin.Parent,
            RequestedByParentId = parent.Id
        };
        db.Lessons.Add(lesson);
        await db.SaveChangesAsync();

        await NotifyTeacherAsync("בקשה לקביעת שיעור",
            $"התקבלה בקשה לקביעת שיעור עבור {student.FullName} בתאריך {FormatDate(lesson.StartTime)}.");

        return CreatedAtAction(nameof(GetById), new { id = lesson.Id }, ToDto(lesson, student.FullName));
    }

    /// <summary>בקשת ביטול/שינוי מועד לשיעור קיים ומתוזמן.</summary>
    [Authorize(Roles = Roles.Parent)]
    [HttpPost("{lessonId:guid}/change-requests")]
    public async Task<ActionResult<ChangeRequestDto>> RequestChange(Guid lessonId, CreateChangeRequestRequest request)
    {
        if (request.Type == ChangeRequestType.Reschedule &&
            (request.ProposedStartTime is null || request.ProposedEndTime is null ||
             request.ProposedEndTime <= request.ProposedStartTime))
            return BadRequest(new { message = "יש לציין מועד חדש תקין לבקשת שינוי." });

        var parent = await CurrentParentAsync();
        if (parent is null) return Forbid();

        var lesson = await db.Lessons.Include(l => l.Student).FirstOrDefaultAsync(l => l.Id == lessonId);
        if (lesson is null) return NotFound();

        var isOwnChild = await db.StudentParents.AnyAsync(sp => sp.ParentId == parent.Id && sp.StudentId == lesson.StudentId);
        if (!isOwnChild) return Forbid();

        if (lesson.Status != LessonStatus.Scheduled)
            return Conflict(new { message = "ניתן לבקש שינוי רק לשיעור מתוזמן." });

        if (await db.LessonChangeRequests.AnyAsync(c => c.LessonId == lessonId && c.Status == ChangeRequestStatus.Pending))
            return Conflict(new { message = "כבר קיימת בקשה ממתינה לשיעור זה." });

        var changeRequest = new LessonChangeRequest
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            LessonId = lessonId,
            RequestedByParentId = parent.Id,
            Type = request.Type,
            ProposedStartTime = request.ProposedStartTime,
            ProposedEndTime = request.ProposedEndTime,
            Reason = request.Reason,
            Status = ChangeRequestStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.LessonChangeRequests.Add(changeRequest);
        await db.SaveChangesAsync();

        var subject = request.Type == ChangeRequestType.Cancel ? "בקשה לביטול שיעור" : "בקשה לעדכון שיעור";
        await NotifyTeacherAsync(subject,
            $"התקבלה {subject} עבור {lesson.Student.FullName} (שיעור בתאריך {FormatDate(lesson.StartTime)}).");

        return Ok(new ChangeRequestDto(
            changeRequest.Id, lesson.Id, lesson.StudentId, lesson.Student.FullName, parent.FullName,
            changeRequest.Type, lesson.StartTime, lesson.EndTime,
            changeRequest.ProposedStartTime, changeRequest.ProposedEndTime,
            changeRequest.Reason, changeRequest.Status, changeRequest.CreatedAt));
    }

    // ===== תלמיד (S2) =====

    [Authorize(Roles = Roles.Student)]
    [HttpGet("my-schedule")]
    public async Task<ActionResult<IEnumerable<StudentLessonDto>>> MySchedule()
    {
        var student = await CurrentStudentAsync();
        if (student is null) return Forbid();

        var lessons = await db.Lessons
            .Where(l => l.StudentId == student.Id)
            .OrderBy(l => l.StartTime)
            .Select(l => new StudentLessonDto(l.Id, l.StartTime, l.EndTime, l.Status, l.Homework))
            .ToListAsync();
        return Ok(lessons);
    }

    // ----- עזר -----

    private static LessonDto ToDto(Lesson l, string studentName) => new(
        l.Id, l.StudentId, studentName, l.StartTime, l.EndTime, l.Status, l.Origin,
        l.Homework, l.PaymentRequired, l.Amount, l.PaymentId != null, l.CompletedAt);

    private static string FormatDate(DateTimeOffset dt) => dt.ToString("dd/MM/yyyy HH:mm");

    private static string BuildEmailHtml(string title, string message) =>
        $"""
        <div dir="rtl" style="font-family:Arial,sans-serif">
          <h2>{WebUtility.HtmlEncode(title)}</h2>
          <p>{WebUtility.HtmlEncode(message)}</p>
        </div>
        """;

    /// <summary>שולח מייל לכל ההורים המקושרים לתלמיד (שינויים ביומן ע"י המורה).</summary>
    private async Task NotifyParentsAsync(Guid studentId, string subject, string message)
    {
        var parentEmails = await db.StudentParents
            .Where(sp => sp.StudentId == studentId)
            .Select(sp => sp.Parent.Email)
            .ToListAsync();

        var html = BuildEmailHtml(subject, message);
        foreach (var email in parentEmails)
        {
            try { await emailSender.SendAsync(email, subject, html); }
            catch (Exception ex) { logger.LogError(ex, "Failed to send lesson notification email."); }
        }
    }

    /// <summary>שולח מייל למורה (בקשות שמגיעות מהורה).</summary>
    private async Task NotifyTeacherAsync(string subject, string message)
    {
        var teacher = await db.Teachers.FirstOrDefaultAsync(t => t.Id == TenantId);
        if (teacher is null) return;

        var user = await userManager.FindByIdAsync(teacher.UserId);
        if (user?.Email is null) return;

        try { await emailSender.SendAsync(user.Email, subject, BuildEmailHtml(subject, message)); }
        catch (Exception ex) { logger.LogError(ex, "Failed to send teacher notification email."); }
    }
}
