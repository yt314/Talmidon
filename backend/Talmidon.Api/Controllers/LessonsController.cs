using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Talmidon.Api.Contracts;
using Talmidon.Api.Services;
using Talmidon.Api.Services.Lessons;
using Talmidon.Domain.Enums;
using Talmidon.Infrastructure.Auth;

namespace Talmidon.Api.Controllers;

/// <summary>
/// יומן השיעורים: ניהול מלא למורה (T4/T5/T6), בקשות ועיון להורה (R2), עיון בלבד לתלמיד (S2).
/// הבקר מתרגם בין HTTP לשירותים בלבד: הכללים, ההרשאה ברמת שורה וההודעות למשפחה
/// נמצאים ב-<see cref="LessonService"/>, ב-<see cref="LessonRequestService"/> וב-<see cref="LessonQueries"/>.
/// </summary>
[ApiController]
[Route("api/lessons")]
public class LessonsController(
    LessonQueries queries,
    LessonService lessons,
    LessonRequestService requests) : ControllerBase
{
    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new InvalidOperationException("No user id in the current context.");

    // ===== מורה: יומן =====

    [Authorize(Roles = Roles.Teacher)]
    [HttpGet]
    public async Task<ActionResult<IEnumerable<LessonDto>>> List(
        [FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to,
        [FromQuery] Guid? studentId, [FromQuery] LessonStatus? status) =>
        Ok(await queries.ListForTeacherAsync(from, to, studentId, status));

    [Authorize(Roles = Roles.Teacher)]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<LessonDto>> GetById(Guid id)
    {
        var lesson = await queries.GetAsync(id);
        return lesson is null ? NotFound() : Ok(lesson);
    }

    [Authorize(Roles = Roles.Teacher)]
    [HttpPost]
    public async Task<ActionResult<LessonDto>> Create(CreateLessonRequest request) =>
        CreatedOr(await lessons.CreateAsync(request));

    [Authorize(Roles = Roles.Teacher)]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateLessonRequest request) =>
        NoContentOr(await lessons.UpdateAsync(id, request));

    [Authorize(Roles = Roles.Teacher)]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id) =>
        NoContentOr(await lessons.DeleteAsync(id));

    /// <summary>T5 — סיום שיעור: סטטוס, תשלום, שיעורי בית, והערה פדגוגית אופציונלית.</summary>
    [Authorize(Roles = Roles.Teacher)]
    [HttpPost("{id:guid}/complete")]
    public async Task<ActionResult<LessonDto>> Complete(Guid id, CompleteLessonRequest request) =>
        OkOr(await lessons.CompleteAsync(id, request));

    /// <summary>סימון "לא הגיע" — שיעור מתוזמן שהתלמיד לא הופיע אליו (ללא חיוב).</summary>
    [Authorize(Roles = Roles.Teacher)]
    [HttpPost("{id:guid}/no-show")]
    public async Task<ActionResult<LessonDto>> MarkNoShow(Guid id) =>
        OkOr(await lessons.MarkNoShowAsync(id));

    // ===== מורה: בקשות שיעור חדש ממתינות (Status=Requested) =====

    [Authorize(Roles = Roles.Teacher)]
    [HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> ApproveRequest(Guid id) =>
        NoContentOr(await lessons.ApproveRequestAsync(id));

    [Authorize(Roles = Roles.Teacher)]
    [HttpPost("{id:guid}/decline")]
    public async Task<IActionResult> DeclineRequest(Guid id) =>
        NoContentOr(await lessons.DeclineRequestAsync(id));

    // ===== מורה: בקשות שינוי/ביטול לשיעור קיים (T6) =====

    [Authorize(Roles = Roles.Teacher)]
    [HttpGet("change-requests")]
    public async Task<ActionResult<IEnumerable<ChangeRequestDto>>> ListChangeRequests([FromQuery] ChangeRequestStatus? status) =>
        Ok(await queries.ListChangeRequestsAsync(status));

    [Authorize(Roles = Roles.Teacher)]
    [HttpPost("change-requests/{id:guid}/approve")]
    public async Task<IActionResult> ApproveChangeRequest(Guid id) =>
        NoContentOr(await requests.ApproveChangeAsync(id));

    [Authorize(Roles = Roles.Teacher)]
    [HttpPost("change-requests/{id:guid}/reject")]
    public async Task<IActionResult> RejectChangeRequest(Guid id) =>
        NoContentOr(await requests.RejectChangeAsync(id));

    // ===== הורה (R2) =====

    [Authorize(Roles = Roles.Parent)]
    [HttpGet("mine")]
    public async Task<ActionResult<IEnumerable<LessonDto>>> MyChildrensLessons([FromQuery] Guid? studentId) =>
        OkOr(await queries.ListForParentAsync(CurrentUserId, studentId));

    /// <summary>בקשת שיעור חדש (נכנס כ-Requested עד אישור המורה).</summary>
    [Authorize(Roles = Roles.Parent)]
    [HttpPost("requests")]
    public async Task<ActionResult<LessonDto>> RequestLesson(CreateLessonRequest request) =>
        CreatedOr(await requests.RequestByParentAsync(CurrentUserId, request));

    /// <summary>בקשת שיעור מהתלמיד עבור עצמו בלבד.</summary>
    [Authorize(Roles = Roles.Student)]
    [HttpPost("my-requests")]
    public async Task<ActionResult<LessonDto>> RequestOwnLesson(CreateStudentLessonRequest request) =>
        CreatedOr(await requests.RequestByStudentAsync(CurrentUserId, request));

    /// <summary>בקשות השינוי של ילדי ההורה — כולל התשובה שהתקבלה עליהן.</summary>
    [Authorize(Roles = Roles.Parent)]
    [HttpGet("my-change-requests")]
    public async Task<ActionResult<IEnumerable<ChangeRequestDto>>> MyChangeRequests() =>
        OkOr(await queries.ListChangeRequestsForParentAsync(CurrentUserId));

    /// <summary>בקשת ביטול/שינוי מועד לשיעור קיים ומתוזמן.</summary>
    [Authorize(Roles = Roles.Parent)]
    [HttpPost("{lessonId:guid}/change-requests")]
    public async Task<ActionResult<ChangeRequestDto>> RequestChange(Guid lessonId, CreateChangeRequestRequest request) =>
        OkOr(await requests.RequestChangeAsync(CurrentUserId, lessonId, request));

    // ===== תלמיד (S2) =====

    [Authorize(Roles = Roles.Student)]
    [HttpGet("my-schedule")]
    public async Task<ActionResult<IEnumerable<StudentLessonDto>>> MySchedule() =>
        OkOr(await queries.ScheduleForStudentAsync(CurrentUserId));

    // ----- עזר -----

    private ActionResult OkOr<T>(ServiceResult<T> result) =>
        result.Error is null ? Ok(result.Value) : this.ToHttp(result.Error);

    private IActionResult NoContentOr(ServiceError? error) =>
        error is null ? NoContent() : this.ToHttp(error);

    private ActionResult CreatedOr(ServiceResult<LessonDto> result) =>
        result.Error is null
            ? CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value)
            : this.ToHttp(result.Error);
}
