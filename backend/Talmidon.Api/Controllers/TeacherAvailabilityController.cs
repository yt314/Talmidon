using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Talmidon.Api.Contracts;
using Talmidon.Api.Services;
using Talmidon.Api.Services.Teachers;
using Talmidon.Infrastructure.Auth;

namespace Talmidon.Api.Controllers;

/// <summary>שעות הזמינות השבועיות של המורה המחוברת.</summary>
[ApiController]
[Authorize(Roles = Roles.Teacher)]
[Route("api/teachers/me/availability")]
public class TeacherAvailabilityController(TeacherAvailabilityService availability) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<AvailabilityWindowDto>>> GetMyAvailability() =>
        Ok(await availability.GetAsync());

    /// <summary>מחליף את כל חלונות הזמינות בבת אחת (עריכת הלוח השבועי ושמירה).</summary>
    [HttpPut]
    public async Task<IActionResult> UpdateMyAvailability(UpdateAvailabilityRequest request)
    {
        var error = await availability.ReplaceAsync(request.Windows);
        return error is null ? NoContent() : this.ToHttp(error);
    }
}
