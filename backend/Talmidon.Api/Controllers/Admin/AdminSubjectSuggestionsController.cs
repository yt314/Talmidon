using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Talmidon.Api.Contracts;
using Talmidon.Api.Services;
using Talmidon.Infrastructure.Auth;

namespace Talmidon.Api.Controllers.Admin;

/// <summary>ניהול הצעות תחומי הלימוד — ראו <see cref="SubjectSuggestions"/>.</summary>
[ApiController]
[Route("api/admin/subject-suggestions")]
[Authorize(Roles = Roles.Admin)]
public class AdminSubjectSuggestionsController(SubjectSuggestions suggestions) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<AdminSubjectSuggestionDto>>> ListSubjectSuggestions() =>
        Ok(await suggestions.ForAdminAsync());

    [HttpPost]
    public async Task<IActionResult> AddSubjectSuggestion(AdminSubjectSuggestionRequest request) =>
        NoContentOr(await suggestions.SetHiddenAsync(request.Name, hidden: false));

    [HttpDelete]
    public async Task<IActionResult> HideSubjectSuggestion([FromQuery] string name) =>
        NoContentOr(await suggestions.SetHiddenAsync(name, hidden: true));

    private IActionResult NoContentOr(ServiceError? error) => error is null ? NoContent() : this.ToHttp(error);
}
