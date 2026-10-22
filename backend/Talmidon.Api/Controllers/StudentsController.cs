using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Talmidon.Api.Contracts;
using Talmidon.Api.Services;
using Talmidon.Api.Services.Students;
using Talmidon.Infrastructure.Auth;

namespace Talmidon.Api.Controllers;

/// <summary>
/// תלמידים: ניהול למורה, ומה שהמשפחה רואה על עצמה. הבקר מתרגם בין HTTP
/// ל-<see cref="StudentService"/> ול-<see cref="StudentQueries"/>.
/// </summary>
[ApiController]
[Route("api/students")]
public class StudentsController(StudentQueries queries, StudentService students) : ControllerBase
{
    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new InvalidOperationException("No user id in the current context.");

    [Authorize(Roles = Roles.Teacher)]
    [HttpGet]
    public async Task<ActionResult<IEnumerable<StudentListItemDto>>> List() =>
        Ok(await queries.ListAsync());

    [Authorize(Roles = Roles.Teacher)]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<StudentDetailDto>> GetById(Guid id)
    {
        var student = await queries.GetAsync(id);
        return student is null ? NotFound() : Ok(student);
    }

    /// <summary>שליחה חוזרת של ההזמנה לתלמיד/ה שיש לו/ה כניסה משלו/ה ועוד לא קבע/ה סיסמה.</summary>
    [Authorize(Roles = Roles.Teacher)]
    [HttpPost("{id:guid}/resend-invitation")]
    public async Task<IActionResult> ResendInvitation(Guid id) =>
        NoContentOr(await students.ResendInvitationAsync(id));

    [Authorize(Roles = Roles.Teacher)]
    [HttpPost]
    public async Task<ActionResult<StudentDetailDto>> Create(CreateStudentRequest request)
    {
        var (studentId, errors) = await students.CreateAsync(request);
        if (studentId is not Guid id) return BadRequest(new { errors });
        return CreatedAtAction(nameof(GetById), new { id }, await queries.GetAsync(id));
    }

    [Authorize(Roles = Roles.Teacher)]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateStudentRequest request) =>
        NoContentOr(await students.UpdateAsync(id, request));

    [Authorize(Roles = Roles.Teacher)]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id) =>
        NoContentOr(await students.DeleteAsync(id));

    // ----- קישור הורה-תלמיד -----

    [Authorize(Roles = Roles.Teacher)]
    [HttpPost("{id:guid}/parents/{parentId:guid}")]
    public async Task<IActionResult> LinkParent(Guid id, Guid parentId) =>
        NoContentOr(await students.LinkParentAsync(id, parentId));

    [Authorize(Roles = Roles.Teacher)]
    [HttpDelete("{id:guid}/parents/{parentId:guid}")]
    public async Task<IActionResult> UnlinkParent(Guid id, Guid parentId) =>
        NoContentOr(await students.UnlinkParentAsync(id, parentId));

    // ===== הורה =====

    [Authorize(Roles = Roles.Parent)]
    [HttpGet("mine")]
    public async Task<ActionResult<IEnumerable<MyChildDto>>> MyChildren()
    {
        var result = await queries.ChildrenOfParentAsync(CurrentUserId);
        return result.Error is null ? Ok(result.Value) : this.ToHttp(result.Error);
    }

    // ===== תלמיד =====

    [Authorize(Roles = Roles.Student)]
    [HttpGet("me")]
    public async Task<ActionResult<MyStudentProfileDto>> MyProfile()
    {
        var profile = await queries.ProfileOfStudentAsync(CurrentUserId);
        return profile is null ? Forbid() : Ok(profile);
    }

    private IActionResult NoContentOr(ServiceError? error) =>
        error is null ? NoContent() : this.ToHttp(error);
}
