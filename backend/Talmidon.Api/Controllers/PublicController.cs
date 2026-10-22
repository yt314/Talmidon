using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Talmidon.Api.Contracts;
using Talmidon.Api.Services;
using Talmidon.Api.Services.Public;

namespace Talmidon.Api.Controllers;

/// <summary>
/// ספריית המורות הציבורית (P1/P2) — ללא התחברות, ותמיד מוגבל למורות עם IsPublic=true
/// (ראו <see cref="PublicLibrary"/>), והפנייה אליהן (<see cref="ContactRequestIntake"/>).
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/public/teachers")]
public class PublicController(PublicLibrary library, ContactRequestIntake contactRequests) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<PublicTeacherSummaryDto>>> List(
        [FromQuery] string? subject, [FromQuery] string? city, [FromQuery] string? search) =>
        Ok(await library.ListAsync(subject, city, search));

    [HttpGet("subjects")]
    public async Task<ActionResult<IEnumerable<string>>> ListSubjects() => Ok(await library.SubjectsAsync());

    [HttpGet("cities")]
    public async Task<ActionResult<IEnumerable<string>>> ListCities() => Ok(await library.CitiesAsync());

    /// <summary>
    /// תמונת הפרופיל. נשמרת במטמון הדפדפן לשנה, וחילוף תמונה עוקף אותו דרך פרמטר
    /// ה-v שבכתובת.
    /// </summary>
    [HttpGet("{id:guid}/photo")]
    public async Task<IActionResult> GetPhoto(Guid id)
    {
        if (await library.PhotoAsync(id) is not { } photo) return NotFound();

        Response.Headers.CacheControl = "public, max-age=31536000, immutable";
        return File(photo.Data, photo.ContentType);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PublicTeacherDetailDto>> GetById(Guid id)
    {
        var teacher = await library.GetAsync(id);
        return teacher is null ? NotFound() : Ok(teacher);
    }

    /// <summary>
    /// פנייה מהספרייה. פתוחה למבקרים, ולכן מוגבלת בקצב לפי כתובת IP — זה טופס
    /// אנונימי שיוצר רשומות במסד ושולח מייל.
    /// </summary>
    [HttpPost("{id:guid}/contact")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Contact(Guid id, CreateContactRequestRequest request)
    {
        var error = await contactRequests.SubmitAsync(id, request);
        return error is null ? NoContent() : this.ToHttp(error);
    }
}
