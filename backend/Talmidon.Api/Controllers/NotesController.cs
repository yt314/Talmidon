using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Talmidon.Api.Contracts;
using Talmidon.Api.Services;
using Talmidon.Infrastructure.Auth;

namespace Talmidon.Api.Controllers;

/// <summary>מעקב פדגוגי — ראו <see cref="NoteService"/>.</summary>
[ApiController]
[Route("api/notes")]
public class NotesController(NoteService notes) : ControllerBase
{
    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new InvalidOperationException("No user id in the current context.");

    // ===== מורה (T8) =====

    [Authorize(Roles = Roles.Teacher)]
    [HttpGet]
    public async Task<ActionResult<IEnumerable<NoteDto>>> List([FromQuery] Guid? studentId) =>
        Ok(await notes.ListAsync(studentId));

    [Authorize(Roles = Roles.Teacher)]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<NoteDto>> GetById(Guid id)
    {
        var note = await notes.GetAsync(id);
        return note is null ? NotFound() : Ok(note);
    }

    [Authorize(Roles = Roles.Teacher)]
    [HttpPost]
    public async Task<ActionResult<NoteDto>> Create(CreateNoteRequest request)
    {
        var result = await notes.CreateAsync(request);
        return result.Error is null
            ? CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value)
            : this.ToHttp(result.Error);
    }

    [Authorize(Roles = Roles.Teacher)]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateNoteRequest request) => NoContentOr(await notes.UpdateAsync(id, request));

    [Authorize(Roles = Roles.Teacher)]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id) => NoContentOr(await notes.DeleteAsync(id));

    // ===== הורה (R4) =====

    [Authorize(Roles = Roles.Parent)]
    [HttpGet("mine")]
    public async Task<ActionResult<IEnumerable<ParentNoteDto>>> MyChildrensNotes([FromQuery] Guid? studentId) =>
        OkOr(await notes.ForParentAsync(CurrentUserId, studentId));

    // ===== תלמיד (S3) =====

    [Authorize(Roles = Roles.Student)]
    [HttpGet("my-notes")]
    public async Task<ActionResult<IEnumerable<StudentNoteDto>>> MyNotes() =>
        OkOr(await notes.ForStudentAsync(CurrentUserId));

    private ActionResult OkOr<T>(ServiceResult<T> result) => result.Error is null ? Ok(result.Value) : this.ToHttp(result.Error);

    private IActionResult NoContentOr(ServiceError? error) => error is null ? NoContent() : this.ToHttp(error);
}
