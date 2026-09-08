using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Talmidon.Api.Contracts;
using Talmidon.Infrastructure.Ai;
using Talmidon.Infrastructure.Auth;

namespace Talmidon.Api.Controllers;

/// <summary>
/// תכונות מבוססות מודל שפה, למורה בלבד.
///
/// הקריאה למודל נעשית מהשרת ולא מהדפדפן: מפתח ה-API לעולם אינו מגיע ללקוח, וכך גם אי
/// אפשר לשרוף את המכסה של המורה מקונסולת הדפדפן.
/// </summary>
[ApiController]
[Authorize(Roles = Roles.Teacher)]
[Route("api/ai")]
public class AiController(ILessonPlanner planner) : ControllerBase
{
    /// <summary>מה זמין. הממשק שואל פעם אחת ומסתיר את מה שלא מוגדר.</summary>
    [HttpGet("availability")]
    public ActionResult<AiAvailabilityDto> Availability() =>
        Ok(new AiAvailabilityDto(planner.IsConfigured));

    [HttpPost("lesson-plan")]
    public async Task<ActionResult<LessonPlanResponse>> BuildLessonPlan(BuildLessonPlanRequest request)
    {
        if (!planner.IsConfigured)
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { message = "בניית מערכי שיעור אינה מוגדרת בשרת." });

        var result = await planner.BuildAsync(new LessonPlanRequest(
            request.Subject.Trim(),
            request.Topic.Trim(),
            request.DurationMinutes,
            string.IsNullOrWhiteSpace(request.GradeLevel) ? null : request.GradeLevel.Trim(),
            string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim()));

        return result.Ok
            ? Ok(new LessonPlanResponse(result.Plan!))
            : BadRequest(new { message = result.Error });
    }
}
