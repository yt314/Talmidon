using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Talmidon.Api.Contracts;
using Talmidon.Api.Services;
using Talmidon.Api.Services.Parents;
using Talmidon.Infrastructure.Auth;

namespace Talmidon.Api.Controllers;

/// <summary>הורים: ניהול למורה, והפרופיל של ההורה עצמו — ראו <see cref="ParentService"/>.</summary>
[ApiController]
[Route("api/parents")]
public class ParentsController(ParentService parents) : ControllerBase
{
    [Authorize(Roles = Roles.Teacher)]
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ParentDto>>> List() => Ok(await parents.ListAsync());

    [Authorize(Roles = Roles.Teacher)]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ParentDto>> GetById(Guid id)
    {
        var parent = await parents.GetAsync(id);
        return parent is null ? NotFound() : Ok(parent);
    }

    [Authorize(Roles = Roles.Teacher)]
    [HttpPost]
    public async Task<ActionResult<ParentDto>> Create(CreateParentRequest request)
    {
        var (parent, errors) = await parents.CreateAsync(request);
        return parent is null
            ? BadRequest(new { errors })
            : CreatedAtAction(nameof(GetById), new { id = parent.Id }, parent);
    }

    /// <summary>שליחה חוזרת של ההזמנה להורה שעוד לא קבע סיסמה.</summary>
    [Authorize(Roles = Roles.Teacher)]
    [HttpPost("{id:guid}/resend-invitation")]
    public async Task<IActionResult> ResendInvitation(Guid id) => NoContentOr(await parents.ResendInvitationAsync(id));

    [Authorize(Roles = Roles.Teacher)]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateParentRequest request) => NoContentOr(await parents.UpdateAsync(id, request));

    [Authorize(Roles = Roles.Teacher)]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id) => NoContentOr(await parents.DeleteAsync(id));

    // ===== הורה =====

    [Authorize(Roles = Roles.Parent)]
    [HttpGet("me")]
    public async Task<ActionResult<MyParentProfileDto>> MyProfile()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("No user id in the current context.");
        var profile = await parents.ProfileOfParentAsync(userId);
        return profile is null ? Forbid() : Ok(profile);
    }

    private IActionResult NoContentOr(ServiceError? error) => error is null ? NoContent() : this.ToHttp(error);
}
