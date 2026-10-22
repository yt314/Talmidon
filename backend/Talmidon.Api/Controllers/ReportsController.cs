using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Talmidon.Api.Contracts;
using Talmidon.Api.Services;
using Talmidon.Infrastructure.Auth;

namespace Talmidon.Api.Controllers;

/// <summary>דוחות חודשיים למורה — ראו <see cref="ReportService"/>.</summary>
[ApiController]
[Authorize(Roles = Roles.Teacher)]
[Route("api/reports")]
public class ReportsController(ReportService reports) : ControllerBase
{
    [HttpGet("income")]
    public async Task<ActionResult<IncomeReportDto>> Income([FromQuery] int? year, [FromQuery] int? month) =>
        OkOr(await reports.IncomeAsync(year, month));

    [HttpGet("attendance")]
    public async Task<ActionResult<AttendanceReportDto>> Attendance([FromQuery] int? year, [FromQuery] int? month) =>
        OkOr(await reports.AttendanceAsync(year, month));

    private ActionResult OkOr<T>(ServiceResult<T> result) =>
        result.Error is null ? Ok(result.Value) : this.ToHttp(result.Error);
}
