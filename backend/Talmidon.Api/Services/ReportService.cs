using Microsoft.EntityFrameworkCore;
using Talmidon.Api.Contracts;
using Talmidon.Domain.Enums;
using Talmidon.Infrastructure.Data;

namespace Talmidon.Api.Services;

/// <summary>דוחות חודשיים למורה. מסונן אוטומטית לדייר הנוכחי ע"י ה-Global Query Filter.</summary>
public class ReportService(TalmidonDbContext db)
{
    /// <summary>דוח הכנסות חודשי: שיעורים שהתקיימו, סכום שחויב, שולם ופתוח, ופילוח לפי תלמיד.</summary>
    public async Task<ServiceResult<IncomeReportDto>> IncomeAsync(int? year, int? month)
    {
        if (Month.From(year, month) is not { } m) return ServiceError.Invalid("חודש לא תקין.");

        var rows = await db.Lessons
            .Where(l => l.Status == LessonStatus.Completed && l.StartTime >= m.Start && l.StartTime < m.End)
            .Select(l => new IncomeRow(l.StudentId, l.Student.FullName, l.PaymentRequired, l.Amount, l.PaymentId != null))
            .ToListAsync();

        return SummarizeIncome(m, rows);
    }

    /// <summary>
    /// דוח נוכחות חודשי: כמה שיעורים התקיימו, בוטלו ולא הגיע, כמה שעות לימוד בפועל,
    /// ופילוח לפי תלמיד לפי שיעור ההחמצות.
    ///
    /// הנתונים האלה נשמרו מאז ומעולם ולא הוצגו בשום מקום — מורה לא יכלה לדעת מי מבטל
    /// הרבה או כמה שעות עבדה החודש.
    /// </summary>
    public async Task<ServiceResult<AttendanceReportDto>> AttendanceAsync(int? year, int? month)
    {
        if (Month.From(year, month) is not { } m) return ServiceError.Invalid("חודש לא תקין.");

        var statuses = new[] { LessonStatus.Completed, LessonStatus.Cancelled, LessonStatus.NoShow };
        var rows = await db.Lessons
            .Where(l => statuses.Contains(l.Status) && l.StartTime >= m.Start && l.StartTime < m.End)
            .Select(l => new AttendanceRow(l.StudentId, l.Student.FullName, l.Status, (l.EndTime - l.StartTime).TotalMinutes))
            .ToListAsync();

        return SummarizeAttendance(m, rows);
    }

    // ----- החודש -----

    /// <summary>חודש קלנדרי ב-UTC; ברירת המחדל היא החודש הנוכחי.</summary>
    private sealed record Month(int Year, int Number, DateTimeOffset Start, DateTimeOffset End)
    {
        public static Month? From(int? year, int? month)
        {
            var now = DateTimeOffset.UtcNow;
            var y = year ?? now.Year;
            var m = month ?? now.Month;
            if (m is < 1 or > 12) return null;

            var start = new DateTimeOffset(new DateTime(y, m, 1, 0, 0, 0, DateTimeKind.Utc), TimeSpan.Zero);
            return new Month(y, m, start, start.AddMonths(1));
        }
    }

    // ----- הכנסות -----

    private sealed record IncomeRow(Guid StudentId, string StudentName, bool PaymentRequired, decimal Amount, bool IsPaid);

    private static IncomeReportDto SummarizeIncome(Month month, List<IncomeRow> rows)
    {
        var byStudent = rows
            .GroupBy(r => (r.StudentId, r.StudentName))
            .Select(g => new StudentIncomeDto(
                g.Key.StudentId,
                g.Key.StudentName,
                g.Count(),
                g.Where(r => r.PaymentRequired).Sum(r => r.Amount),
                g.Where(r => r.PaymentRequired && r.IsPaid).Sum(r => r.Amount)))
            .OrderByDescending(s => s.Charged)
            .ThenBy(s => s.StudentName)
            .ToList();

        var charged = byStudent.Sum(s => s.Charged);
        var paid = byStudent.Sum(s => s.Paid);
        return new IncomeReportDto(month.Year, month.Number, rows.Count, charged, paid, charged - paid, byStudent);
    }

    // ----- נוכחות -----

    private sealed record AttendanceRow(Guid StudentId, string StudentName, LessonStatus Status, double Minutes);

    private static AttendanceReportDto SummarizeAttendance(Month month, List<AttendanceRow> rows)
    {
        var byStudent = rows
            .GroupBy(r => (r.StudentId, r.StudentName))
            .Select(g => StudentAttendance(g.Key.StudentId, g.Key.StudentName, g.ToList()))
            .OrderByDescending(s => s.MissedPercent)
            .ThenBy(s => s.StudentName)
            .ToList();

        return new AttendanceReportDto(
            month.Year, month.Number,
            rows.Count(r => r.Status == LessonStatus.Completed),
            rows.Count(r => r.Status == LessonStatus.Cancelled),
            rows.Count(r => r.Status == LessonStatus.NoShow),
            CompletedHours(rows),
            byStudent);
    }

    private static StudentAttendanceDto StudentAttendance(Guid studentId, string studentName, List<AttendanceRow> rows)
    {
        var completed = rows.Count(r => r.Status == LessonStatus.Completed);
        var cancelled = rows.Count(r => r.Status == LessonStatus.Cancelled);
        var noShow = rows.Count(r => r.Status == LessonStatus.NoShow);
        var total = completed + cancelled + noShow;
        var missedPercent = total == 0 ? 0 : (int)Math.Round((cancelled + noShow) * 100.0 / total);
        return new StudentAttendanceDto(studentId, studentName, completed, cancelled, noShow, CompletedHours(rows), missedPercent);
    }

    private static decimal CompletedHours(IEnumerable<AttendanceRow> rows) =>
        Math.Round((decimal)rows.Where(r => r.Status == LessonStatus.Completed).Sum(r => r.Minutes) / 60m, 1);
}
