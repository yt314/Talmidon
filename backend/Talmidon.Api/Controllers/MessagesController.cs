using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Talmidon.Api.Contracts;
using Talmidon.Api.Services;
using Talmidon.Api.Services.Messages;
using Talmidon.Domain.Enums;
using Talmidon.Infrastructure.Auth;

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
///
/// הבקר מתרגם בין HTTP לשירותים בלבד: <see cref="MessageQueries"/>,
/// <see cref="TeacherMessageService"/> ו-<see cref="FamilyMessageService"/>.
/// </summary>
[ApiController]
[Route("api/messages")]
public class MessagesController(
    MessageQueries queries,
    MessageThreads threads,
    TeacherMessageService teacher,
    FamilyMessageService family) : ControllerBase
{
    // ===== מורה =====

    /// <summary>התיבה של המורה, מהשיחה שזזה אחרונה. שיחות סגורות יורדות לסוף.</summary>
    [Authorize(Roles = Roles.Teacher)]
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ThreadSummaryDto>>> Inbox(
        [FromQuery] Guid? studentId, [FromQuery] bool includeClosed = true) =>
        Ok(await queries.InboxAsync(studentId, includeClosed));

    /// <summary>מונה לתג שליד תיבת הדואר בסרגל העליון.</summary>
    [Authorize(Roles = Roles.Teacher)]
    [HttpGet("unread-count")]
    public async Task<ActionResult<UnreadCountDto>> TeacherUnreadCount() =>
        Ok(new UnreadCountDto(await queries.TeacherUnreadCountAsync()));

    [Authorize(Roles = Roles.Teacher)]
    [HttpGet("recipients")]
    public async Task<ActionResult<IEnumerable<MessageRecipientDto>>> Recipients() =>
        Ok(await queries.RecipientsAsync());

    [Authorize(Roles = Roles.Teacher)]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ThreadDetailDto>> GetThread(Guid id) =>
        OkOr(await teacher.OpenAsync(id));

    /// <summary>המורה פותחת שיחה מיוזמתה.</summary>
    [Authorize(Roles = Roles.Teacher)]
    [HttpPost]
    public async Task<ActionResult<ThreadDetailDto>> StartThread(TeacherStartThreadRequest request) =>
        CreatedOr(nameof(GetThread), await teacher.StartAsync(request));

    [Authorize(Roles = Roles.Teacher)]
    [HttpPost("{id:guid}/reply")]
    public async Task<ActionResult<MessageDto>> TeacherReply(Guid id, PostMessageRequest request) =>
        OkOr(await teacher.ReplyAsync(id, request));

    /// <summary>סגירת שיחה שטופלה. היא נשארת לקריאה, והודעה חדשה תפתח אותה מחדש.</summary>
    [Authorize(Roles = Roles.Teacher)]
    [HttpPost("{id:guid}/close")]
    public async Task<IActionResult> Close(Guid id) => NoContentOr(await teacher.SetClosedAsync(id, true));

    [Authorize(Roles = Roles.Teacher)]
    [HttpPost("{id:guid}/reopen")]
    public async Task<IActionResult> Reopen(Guid id) => NoContentOr(await teacher.SetClosedAsync(id, false));

    // ===== תלמידה והורה =====

    [Authorize(Roles = $"{Roles.Student},{Roles.Parent}")]
    [HttpGet("mine")]
    public async Task<ActionResult<IEnumerable<ThreadSummaryDto>>> MyThreads()
    {
        var me = await CurrentCounterpartAsync();
        return me is null ? Forbid() : Ok(await queries.ThreadsOfAsync(me.Value));
    }

    [Authorize(Roles = $"{Roles.Student},{Roles.Parent}")]
    [HttpGet("mine/unread-count")]
    public async Task<ActionResult<UnreadCountDto>> MyUnreadCount()
    {
        var me = await CurrentCounterpartAsync();
        return me is null ? Forbid() : Ok(new UnreadCountDto(await queries.UnreadCountOfAsync(me.Value)));
    }

    [Authorize(Roles = $"{Roles.Student},{Roles.Parent}")]
    [HttpGet("mine/{id:guid}")]
    public async Task<ActionResult<ThreadDetailDto>> MyThread(Guid id)
    {
        var me = await CurrentCounterpartAsync();
        return me is null ? Forbid() : OkOr(await family.OpenAsync(me.Value, id));
    }

    /// <summary>פנייה חדשה למורה, מתלמידה בעניין עצמה או מהורה בעניין אחד מילדיו.</summary>
    [Authorize(Roles = $"{Roles.Student},{Roles.Parent}")]
    [HttpPost("mine")]
    public async Task<ActionResult<ThreadDetailDto>> StartMyThread(StartThreadRequest request)
    {
        var me = await CurrentCounterpartAsync();
        return me is null ? Forbid() : CreatedOr(nameof(MyThread), await family.StartAsync(me.Value, request));
    }

    [Authorize(Roles = $"{Roles.Student},{Roles.Parent}")]
    [HttpPost("mine/{id:guid}/reply")]
    public async Task<ActionResult<MessageDto>> MyReply(Guid id, PostMessageRequest request)
    {
        var me = await CurrentCounterpartAsync();
        return me is null ? Forbid() : OkOr(await family.ReplyAsync(me.Value, id, request));
    }

    // ----- עזר -----

    /// <summary>מי מדבר מול המורה כרגע — לפי התפקיד שבטוקן, לא לפי מה שנשלח בבקשה.</summary>
    private Task<Counterpart?> CurrentCounterpartAsync()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("No user id in the current context.");
        MessageAuthor? role = User.IsInRole(Roles.Student) ? MessageAuthor.Student
            : User.IsInRole(Roles.Parent) ? MessageAuthor.Parent
            : null;
        return threads.CurrentCounterpartAsync(userId, role);
    }

    private ActionResult OkOr<T>(ServiceResult<T> result) =>
        result.Error is null ? Ok(result.Value) : this.ToHttp(result.Error);

    private IActionResult NoContentOr(ServiceError? error) =>
        error is null ? NoContent() : this.ToHttp(error);

    private ActionResult CreatedOr(string action, ServiceResult<ThreadDetailDto> result) =>
        result.Error is null
            ? CreatedAtAction(action, new { id = result.Value!.Id }, result.Value)
            : this.ToHttp(result.Error);
}
