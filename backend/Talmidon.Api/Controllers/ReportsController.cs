using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Talmidon.Api.Contracts;
using Talmidon.Domain.Enums;
using Talmidon.Infrastructure.Auth;
using Talmidon.Infrastructure.Data;

namespace Talmidon.Api.Controllers;

/// <summary>דוחות למורה. מסונן אוטומטית לדייר הנוכחי ע"י ה-Global Query Filter.</summary>
[ApiController]
[Authorize(Roles = Roles.Teacher)]
[Route("api/reports")]
public class ReportsController(TalmidonDbContext db) : ControllerBase
{
    /// <summary>דוח הכנסות חודשי: שיעורים שהתקיימו, סכום שחויב, שולם ופתוח, ופילוח לפי תלמיד.</summary>
    [HttpGet("income")]
    public async Task<ActionResult<IncomeReportDto>> Income([FromQuery] int? year, [FromQuery] int? month)
    {
        var now = DateTimeOffset.UtcNow;
        var y = year ?? now.Year;
        var m = month ?? now.Month;
        if (m is < 1 or > 12) return BadRequest(new { message = "חודש לא תקין." });

        var start = new DateTimeOffset(new DateTime(y, m, 1, 0, 0, 0, DateTimeKind.Utc), TimeSpan.Zero);
        var end = start.AddMonths(1);

        var rows = await db.Lessons
            .Where(l => l.Status == LessonStatus.Completed && l.StartTime >= start && l.StartTime < end)
            .Select(l => new
            {
                l.StudentId,
                StudentName = l.Student.FullName,
                l.PaymentRequired,
                l.Amount,
                IsPaid = l.PaymentId != null
            })
            .ToListAsync();

        var byStudent = rows
            .GroupBy(r => new { r.StudentId, r.StudentName })
            .Select(g => new StudentIncomeDto(
                g.Key.StudentId,
                g.Key.StudentName,
                g.Count(),
                g.Where(r => r.PaymentRequired).Sum(r => r.Amount),
                g.Where(r => r.PaymentRequired && r.IsPaid).Sum(r => r.Amount)))
            .OrderByDescending(s => s.Charged)
            .ThenBy(s => s.StudentName)
            .ToList();

        var totalCharged = byStudent.Sum(s => s.Charged);
        var totalPaid = byStudent.Sum(s => s.Paid);

        return Ok(new IncomeReportDto(
            y, m, rows.Count, totalCharged, totalPaid, totalCharged - totalPaid, byStudent));
    }
}
