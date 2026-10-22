using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Talmidon.Api.Services;
using Talmidon.Domain.Common;
using Talmidon.Infrastructure.Auth;
using Talmidon.Infrastructure.Export;

namespace Talmidon.Api.Controllers;

/// <summary>ייצוא הנתונים של המורה כקובץ CSV — הטבלאות עצמן ב-<see cref="ExportService"/>.</summary>
[ApiController]
[Authorize(Roles = Roles.Teacher)]
[Route("api/export")]
public class ExportController(ExportService export) : ControllerBase
{
    [HttpGet("students.csv")]
    public async Task<IActionResult> Students() => CsvFile(await export.StudentsAsync());

    [HttpGet("lessons.csv")]
    public async Task<IActionResult> Lessons([FromQuery] DateOnly? from, [FromQuery] DateOnly? to) =>
        CsvFile(await export.LessonsAsync(from, to));

    [HttpGet("payments.csv")]
    public async Task<IActionResult> Payments([FromQuery] DateOnly? from, [FromQuery] DateOnly? to) =>
        CsvFile(await export.PaymentsAsync(from, to));

    /// <summary>
    /// שם הקובץ נשלח גם כ-ASCII וגם מקודד: שם בעברית ב-filename הרגיל נשבר אצל חלק
    /// מהדפדפנים, ו-filename* הוא מה שמי שתומך בו קורא.
    /// </summary>
    private FileContentResult CsvFile(ExportTable table)
    {
        var stamp = AppTimeZone.Today.ToString("yyyy-MM-dd");
        var fileName = $"talmidon-{Ascii(table.Name)}-{stamp}.csv";
        var pretty = Uri.EscapeDataString($"תלמידון - {table.Name} - {stamp}.csv");

        Response.Headers.ContentDisposition = $"attachment; filename=\"{fileName}\"; filename*=UTF-8''{pretty}";
        return File(Csv.Build(table.Headers, table.Rows), "text/csv; charset=utf-8");
    }

    private static string Ascii(string name) => name switch
    {
        "תלמידות" => "students",
        "שיעורים" => "lessons",
        "תשלומים" => "payments",
        _ => "export"
    };
}
