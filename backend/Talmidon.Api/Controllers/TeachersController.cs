using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Talmidon.Api.Contracts;
using Talmidon.Api.Services;
using Talmidon.Api.Services.Teachers;
using Talmidon.Infrastructure.Auth;

namespace Talmidon.Api.Controllers;

/// <summary>
/// פרופיל המורה עצמה: דף הכללים + הגדרות שמוצגות בספרייה הציבורית (T9), התחומים,
/// ההצעות להשלמה האוטומטית של התחומים, ותמונת הפרופיל. שעות הזמינות —
/// ב-<see cref="TeacherAvailabilityController"/>.
/// </summary>
[ApiController]
[Authorize(Roles = Roles.Teacher)]
[Route("api/teachers")]
public class TeachersController(TeacherProfileService profile) : ControllerBase
{
    [HttpGet("me")]
    public async Task<ActionResult<TeacherProfileDto>> GetMyProfile()
    {
        var dto = await profile.GetAsync();
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpPut("me")]
    public async Task<IActionResult> UpdateMyProfile(UpdateTeacherProfileRequest request) =>
        NoContentOr(await profile.UpdateAsync(request));

    // ===== תחומים =====

    [HttpPost("me/subjects")]
    public async Task<ActionResult<SubjectDto>> AddSubject(AddSubjectRequest request)
    {
        var result = await profile.AddSubjectAsync(request.Name);
        return result.Error is null ? Ok(result.Value) : this.ToHttp(result.Error);
    }

    [HttpDelete("me/subjects/{id:guid}")]
    public async Task<IActionResult> DeleteSubject(Guid id) => NoContentOr(await profile.DeleteSubjectAsync(id));

    /// <summary>מחליף את כל רשימת התחומים בבת אחת. שמות ריקים וכפילויות מסוננים.</summary>
    [HttpPut("me/subjects")]
    public async Task<ActionResult<IEnumerable<SubjectDto>>> SetSubjects(SetSubjectsRequest request) =>
        Ok(await profile.SetSubjectsAsync(request.Names));

    /// <summary>
    /// הצעות להשלמה אוטומטית: הקטלוג הקבוע יחד עם תחומים שמורות אחרות כבר הזינו,
    /// כך שהרשימה גדלה מעצמה עם השימוש. אינה רשימה סגורה — אפשר להקליד כל תחום.
    /// </summary>
    [HttpGet("subject-suggestions")]
    public async Task<ActionResult<IEnumerable<string>>> SubjectSuggestions([FromServices] SubjectSuggestions suggestions) =>
        Ok(await suggestions.ForTeachersAsync());

    // ===== תמונת פרופיל =====

    [HttpPost("me/photo")]
    [RequestSizeLimit(TeacherProfileService.MaxPhotoBytes + 8192)]
    public async Task<ActionResult<object>> UploadPhoto(IFormFile file)
    {
        var result = await profile.SetPhotoAsync(file);
        return result.Error is null ? Ok(new { photoVersion = result.Value }) : this.ToHttp(result.Error);
    }

    [HttpDelete("me/photo")]
    public async Task<IActionResult> DeletePhoto() => NoContentOr(await profile.ClearPhotoAsync());

    private IActionResult NoContentOr(ServiceError? error) => error is null ? NoContent() : this.ToHttp(error);
}
