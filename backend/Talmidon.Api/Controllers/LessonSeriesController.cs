using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Talmidon.Api.Contracts;
using Talmidon.Api.Services;
using Talmidon.Api.Services.Lessons;
using Talmidon.Infrastructure.Auth;

namespace Talmidon.Api.Controllers;

/// <summary>סדרות שיעורים חוזרות שבועיות (T4) — ראו <see cref="LessonSeriesService"/>.</summary>
[ApiController]
[Route("api/lesson-series")]
[Authorize(Roles = Roles.Teacher)]
public class LessonSeriesController(LessonSeriesService series) : ControllerBase
{
    /// <summary>יוצרת סדרה ומייצרת מיד את המופעים הראשונים, כדי שהמורה תראה שיעורים בלוח מיידית.</summary>
    [HttpPost]
    public async Task<ActionResult<LessonSeriesDto>> Create(CreateLessonSeriesRequest request)
    {
        var result = await series.CreateAsync(request);
        return result.Error is null ? Ok(result.Value) : this.ToHttp(result.Error);
    }

    /// <summary>מבטלת סדרה, ובבחירה גם את השיעורים העתידיים שכבר נקבעו ממנה.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<CancelLessonSeriesResultDto>> Cancel(Guid id, [FromQuery] bool deleteFutureOccurrences = false)
    {
        var result = await series.CancelAsync(id, deleteFutureOccurrences);
        return result.Error is null ? Ok(new CancelLessonSeriesResultDto(result.Value)) : this.ToHttp(result.Error);
    }
}
