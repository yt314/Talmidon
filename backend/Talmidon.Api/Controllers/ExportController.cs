using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Talmidon.Domain.Common;
using Talmidon.Domain.Enums;
using Talmidon.Infrastructure.Auth;
using Talmidon.Infrastructure.Data;
using Talmidon.Infrastructure.Export;

namespace Talmidon.Api.Controllers;

/// <summary>
/// ייצוא הנתונים של המורה כקובץ.
///
/// קיים כי הנתונים שלה אינם אמורים להיות לכודים כאן: רשימת התשלומים נדרשת לדוח שנתי,
/// רשימת התלמידות היא גיבוי, ומורה שתרצה יום אחד לעבוד אחרת צריכה לצאת עם מה שהיא
/// הכניסה. מוגבל לדייר הנוכחי ע"י ה-Global Query Filter, כמו כל השאר.
/// </summary>
[ApiController]
[Authorize(Roles = Roles.Teacher)]
[Route("api/export")]
public class ExportController(TalmidonDbContext db) : ControllerBase
{
    [HttpGet("students.csv")]
    public async Task<IActionResult> Students()
    {
        var students = await db.Students
            .OrderBy(s => s.FullName)
            .Select(s => new
            {
                s.FullName,
                s.GradeLevel,
                s.BirthDate,
                s.IsActive,
                s.DefaultPricePerLesson,
                s.DefaultDurationMinutes,
                s.GeneralInfo,
                s.CreatedAt,
                Parents = s.StudentParents.Select(sp => sp.Parent.FullName).ToList(),
                Phones = s.StudentParents.Select(sp => sp.Parent.Phone).ToList(),
                Emails = s.StudentParents.Select(sp => sp.Parent.Email).ToList()
            })
            .ToListAsync();

        var rows = students.Select(s => new[]
        {
            s.FullName,
            s.GradeLevel,
            s.BirthDate?.ToString("dd/MM/yyyy"),
            s.IsActive ? "פעילה" : "לא פעילה",
            s.DefaultPricePerLesson?.ToString(),
            s.DefaultDurationMinutes?.ToString(),
            string.Join(" | ", s.Parents),
            string.Join(" | ", s.Phones.Where(p => !string.IsNullOrWhiteSpace(p))),
            string.Join(" | ", s.Emails),
            s.GeneralInfo,
            AppTimeZone.ToLocal(s.CreatedAt).ToString("dd/MM/yyyy")
        });

        return CsvFile(
            ["שם", "כיתה", "תאריך לידה", "סטטוס", "מחיר לשיעור", "משך (דקות)",
             "הורים", "טלפונים", "מיילים", "מידע כללי", "נוצרה בתאריך"],
            rows, "תלמידות");
    }

    [HttpGet("lessons.csv")]
    public async Task<IActionResult> Lessons([FromQuery] DateOnly? from, [FromQuery] DateOnly? to)
    {
        var query = db.Lessons.AsQueryable();

        // הגבולות מחושבים כאן ולא בתוך התנאי: המרת אזור זמן אינה משהו שהמסד יודע
        // לבצע, ושאילתה שמכילה אותה נכשלת בזמן ריצה ולא בהידור.
        if (from is DateOnly start)
        {
            var fromUtc = AppTimeZone.ToUtc(start, TimeOnly.MinValue);
            query = query.Where(l => l.StartTime >= fromUtc);
        }

        if (to is DateOnly end)
        {
            var toUtc = AppTimeZone.ToUtc(end.AddDays(1), TimeOnly.MinValue);
            query = query.Where(l => l.StartTime < toUtc);
        }

        var lessons = await query
            .OrderBy(l => l.StartTime)
            .Select(l => new
            {
                l.StartTime,
                l.EndTime,
                StudentName = l.Student.FullName,
                l.Status,
                l.Origin,
                l.Amount,
                l.PaymentRequired,
                Paid = l.PaymentId != null,
                l.Homework
            })
            .ToListAsync();

        var rows = lessons.Select(l => new[]
        {
            AppTimeZone.ToLocal(l.StartTime).ToString("dd/MM/yyyy"),
            AppTimeZone.ToLocal(l.StartTime).ToString("HH:mm"),
            AppTimeZone.ToLocal(l.EndTime).ToString("HH:mm"),
            ((int)(l.EndTime - l.StartTime).TotalMinutes).ToString(),
            l.StudentName,
            StatusLabel(l.Status),
            OriginLabel(l.Origin),
            l.PaymentRequired ? l.Amount.ToString() : "",
            l.PaymentRequired ? (l.Paid ? "שולם" : "לא שולם") : "ללא חיוב",
            l.Homework
        });

        return CsvFile(
            ["תאריך", "התחלה", "סיום", "משך (דקות)", "תלמידה", "סטטוס", "מקור", "סכום", "תשלום", "שיעורי בית"],
            rows, "שיעורים");
    }

    [HttpGet("payments.csv")]
    public async Task<IActionResult> Payments([FromQuery] DateOnly? from, [FromQuery] DateOnly? to)
    {
        var query = db.Payments.AsQueryable();
        if (from is DateOnly start) query = query.Where(p => p.PaidDate >= start);
        if (to is DateOnly end) query = query.Where(p => p.PaidDate <= end);

        var payments = await query
            .OrderBy(p => p.PaidDate)
            .Select(p => new
            {
                p.PaidDate,
                ParentName = p.Parent.FullName,
                p.Amount,
                p.Method,
                p.Note,
                LessonCount = p.CoveredLessons.Count,
                Students = p.CoveredLessons.Select(l => l.Student.FullName).Distinct().ToList()
            })
            .ToListAsync();

        var rows = payments.Select(p => new[]
        {
            p.PaidDate.ToString("dd/MM/yyyy"),
            p.ParentName,
            p.Amount.ToString(),
            p.Method,
            p.LessonCount.ToString(),
            string.Join(" | ", p.Students),
            p.Note
        });

        return CsvFile(["תאריך", "שולם על ידי", "סכום", "אמצעי", "מספר שיעורים", "עבור", "הערה"], rows, "תשלומים");
    }

    // ----- עזר -----

    /// <summary>
    /// שם הקובץ נשלח גם כ-ASCII וגם מקודד: שם בעברית ב-filename הרגיל נשבר אצל חלק
    /// מהדפדפנים, ו-filename* הוא מה שמי שתומך בו קורא.
    /// </summary>
    private FileContentResult CsvFile(string[] headers, IEnumerable<IEnumerable<string?>> rows, string name)
    {
        var stamp = AppTimeZone.Today.ToString("yyyy-MM-dd");
        var fileName = $"talmidon-{Ascii(name)}-{stamp}.csv";
        var pretty = Uri.EscapeDataString($"תלמידון - {name} - {stamp}.csv");

        Response.Headers.ContentDisposition = $"attachment; filename=\"{fileName}\"; filename*=UTF-8''{pretty}";
        return File(Csv.Build(headers, rows), "text/csv; charset=utf-8");
    }

    private static string Ascii(string name) => name switch
    {
        "תלמידות" => "students",
        "שיעורים" => "lessons",
        "תשלומים" => "payments",
        _ => "export"
    };

    private static string StatusLabel(LessonStatus status) => status switch
    {
        LessonStatus.Requested => "ממתין לאישור",
        LessonStatus.Scheduled => "מתוזמן",
        LessonStatus.Completed => "התקיים",
        LessonStatus.Cancelled => "בוטל",
        LessonStatus.Declined => "נדחה",
        LessonStatus.NoShow => "לא הגיעה",
        _ => status.ToString()
    };

    private static string OriginLabel(LessonOrigin origin) => origin switch
    {
        LessonOrigin.Teacher => "המורה",
        LessonOrigin.Parent => "הורה",
        LessonOrigin.Student => "תלמידה",
        _ => origin.ToString()
    };
}
