using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Talmidon.Api.Contracts;
using Talmidon.Api.Services;
using Talmidon.Infrastructure.Auth;

namespace Talmidon.Api.Controllers.Admin;

/// <summary>
/// תפקיד-העל המינימלי לתחזוקת הפלטפורמה (ראו נספח האפיון). היקף מכוון: רשימת מורות
/// + נעילה/שחרור של חשבון — אין כאן שכפול/הרחבה של מודל ההרשאות.
/// </summary>
[ApiController]
[Route("api/admin/teachers")]
[Authorize(Roles = Roles.Admin)]
public class AdminTeachersController(TeacherAccounts accounts) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<AdminTeacherDto>>> ListTeachers() => Ok(await accounts.ListAsync());

    [HttpPost("{id:guid}/lock")]
    public async Task<IActionResult> LockTeacher(Guid id) => NoContentOr(await accounts.LockAsync(id));

    [HttpPost("{id:guid}/unlock")]
    public async Task<IActionResult> UnlockTeacher(Guid id) => NoContentOr(await accounts.UnlockAsync(id));

    private IActionResult NoContentOr(ServiceError? error) => error is null ? NoContent() : this.ToHttp(error);
}
