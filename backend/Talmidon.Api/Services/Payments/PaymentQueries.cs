using Microsoft.EntityFrameworkCore;
using Talmidon.Api.Contracts;
using Talmidon.Infrastructure.Data;

namespace Talmidon.Api.Services.Payments;

/// <summary>
/// קריאות התשלומים: חיובים פתוחים (שיעור שסומן לתשלום ועוד לא שולם), תשלומים שנרשמו,
/// וסיכום "מי חייב כמה". ההגבלה לדייר — ב-Global Query Filter; להורה הספציפי — כאן.
/// </summary>
public class PaymentQueries(TalmidonDbContext db, FamilyMembers family)
{
    public Task<List<OpenChargeDto>> OpenChargesAsync(Guid? studentId, Guid? parentId)
    {
        var query = db.Lessons.Where(l => l.PaymentRequired && l.PaymentId == null);
        if (studentId is not null) query = query.Where(l => l.StudentId == studentId);
        if (parentId is not null)
        {
            var childIds = db.StudentParents.Where(sp => sp.ParentId == parentId).Select(sp => sp.StudentId);
            query = query.Where(l => childIds.Contains(l.StudentId));
        }

        return query
            .OrderBy(l => l.StartTime)
            .Select(l => new OpenChargeDto(l.Id, l.StudentId, l.Student.FullName, l.StartTime, l.Amount))
            .ToListAsync();
    }

    /// <summary>
    /// "מי חייב כמה" — סיכום החיובים הפתוחים לפי הורה, כדי שהמורה תראה את התמונה
    /// בלי לבחור הורה־הורה. תלמיד המשויך לשני הורים נספר פעם אחת בלבד, אצל ההורה
    /// הראשון בסדר אלפביתי, כדי שהסכומים יסתכמו לסך החוב האמיתי.
    /// </summary>
    public async Task<List<OpenChargeSummaryDto>> OpenChargesSummaryAsync()
    {
        var charges = await db.Lessons
            .Where(l => l.PaymentRequired && l.PaymentId == null)
            .Select(l => new OpenChargeRow(
                l.StudentId,
                l.StartTime,
                l.Amount,
                l.Student.StudentParents.Select(sp => new PayerCandidate(sp.ParentId, sp.Parent.FullName)).ToList()))
            .ToListAsync();

        return SummarizeByPayer(charges);
    }

    public Task<List<PaymentDto>> ListAsync(Guid? parentId)
    {
        var query = db.Payments.AsQueryable();
        if (parentId is not null) query = query.Where(p => p.ParentId == parentId);

        return query
            .OrderByDescending(p => p.PaidDate)
            .Select(p => new PaymentDto(
                p.Id, p.ParentId, p.Parent.FullName, p.Amount, p.PaidDate, p.Method, p.Note,
                p.CoveredLessons.Count, p.ConfirmationSentAt))
            .ToListAsync();
    }

    public Task<PaymentDetailDto?> GetAsync(Guid id) =>
        db.Payments
            .Where(p => p.Id == id)
            .Select(p => new PaymentDetailDto(
                p.Id, p.ParentId, p.Parent.FullName, p.Amount, p.PaidDate, p.Method, p.Note, p.ConfirmationSentAt,
                p.CoveredLessons
                    .Select(l => new PaymentLessonDto(l.Id, l.StudentId, l.Student.FullName, l.StartTime, l.Amount))
                    .ToList()))
            .FirstOrDefaultAsync();

    // ===== הורה (R3) =====

    public async Task<ServiceResult<List<OpenChargeDto>>> OpenChargesForParentAsync(string userId)
    {
        var parent = await family.ParentAsync(userId);
        if (parent is null) return ServiceError.Forbidden;

        var childIds = family.ChildIdsOf(parent);
        return await db.Lessons
            .Where(l => l.PaymentRequired && l.PaymentId == null && childIds.Contains(l.StudentId))
            .OrderBy(l => l.StartTime)
            .Select(l => new OpenChargeDto(l.Id, l.StudentId, l.Student.FullName, l.StartTime, l.Amount))
            .ToListAsync();
    }

    public async Task<ServiceResult<List<PaymentDto>>> PaymentsOfParentAsync(string userId)
    {
        var parent = await family.ParentAsync(userId);
        if (parent is null) return ServiceError.Forbidden;

        return await db.Payments
            .Where(p => p.ParentId == parent.Id)
            .OrderByDescending(p => p.PaidDate)
            .Select(p => new PaymentDto(
                p.Id, p.ParentId, parent.FullName, p.Amount, p.PaidDate, p.Method, p.Note,
                p.CoveredLessons.Count, p.ConfirmationSentAt))
            .ToListAsync();
    }

    // ----- הסיכום לפי משלם -----

    private sealed record PayerCandidate(Guid ParentId, string FullName);

    private sealed record OpenChargeRow(Guid StudentId, DateTimeOffset StartTime, decimal Amount, List<PayerCandidate> Parents);

    private static List<OpenChargeSummaryDto> SummarizeByPayer(IEnumerable<OpenChargeRow> charges) =>
        charges
            .Select(c => (Payer: c.Parents.OrderBy(p => p.FullName).ThenBy(p => p.ParentId).FirstOrDefault(), Charge: c))
            .GroupBy(x => (x.Payer?.ParentId, ParentName: x.Payer?.FullName ?? "ללא הורה משויך"))
            .Select(g => new OpenChargeSummaryDto(
                g.Key.ParentId,
                g.Key.ParentName,
                g.Select(x => x.Charge.StudentId).Distinct().Count(),
                g.Count(),
                g.Sum(x => x.Charge.Amount),
                g.Min(x => x.Charge.StartTime)))
            .OrderBy(s => s.OldestLessonStartTime)
            .ToList();
}
