using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Talmidon.Api.Contracts;
using Talmidon.Api.Services;
using Talmidon.Api.Services.Payments;
using Talmidon.Infrastructure.Auth;
using Talmidon.Infrastructure.BackgroundJobs;

namespace Talmidon.Api.Controllers;

/// <summary>
/// תשלומים: חיוב לפי שיעור (לא מנוי חודשי). הבקר מתרגם בין HTTP לשירותים —
/// הכללים והמייל ב-<see cref="PaymentService"/>, הקריאות ב-<see cref="PaymentQueries"/>.
/// </summary>
[ApiController]
[Route("api/payments")]
public class PaymentsController(
    PaymentQueries queries,
    PaymentService payments,
    MonthlyPaymentReminderJob monthlyReminderJob) : ControllerBase
{
    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new InvalidOperationException("No user id in the current context.");

    // ===== מורה =====

    [Authorize(Roles = Roles.Teacher)]
    [HttpGet("open-charges")]
    public async Task<ActionResult<IEnumerable<OpenChargeDto>>> OpenCharges(
        [FromQuery] Guid? studentId, [FromQuery] Guid? parentId) =>
        Ok(await queries.OpenChargesAsync(studentId, parentId));

    /// <summary>"מי חייב כמה" — סיכום החיובים הפתוחים לפי הורה.</summary>
    [Authorize(Roles = Roles.Teacher)]
    [HttpGet("open-charges/summary")]
    public async Task<ActionResult<IEnumerable<OpenChargeSummaryDto>>> OpenChargesSummary() =>
        Ok(await queries.OpenChargesSummaryAsync());

    [Authorize(Roles = Roles.Teacher)]
    [HttpGet]
    public async Task<ActionResult<IEnumerable<PaymentDto>>> List([FromQuery] Guid? parentId) =>
        Ok(await queries.ListAsync(parentId));

    [Authorize(Roles = Roles.Teacher)]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PaymentDetailDto>> GetById(Guid id)
    {
        var payment = await queries.GetAsync(id);
        return payment is null ? NotFound() : Ok(payment);
    }

    /// <summary>מסמנת שיעורים פתוחים כ"שולם" ע"י הורה מסוים, ושולחת מייל אישור.</summary>
    [Authorize(Roles = Roles.Teacher)]
    [HttpPost]
    public async Task<ActionResult<PaymentDto>> Create(CreatePaymentRequest request)
    {
        var result = await payments.CreateAsync(request);
        return result.Error is null
            ? CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value)
            : this.ToHttp(result.Error);
    }

    /// <summary>ביטול סימון תשלום בטעות: משחרר את השיעורים חזרה לפתוחים ומוחק את הרשומה.</summary>
    [Authorize(Roles = Roles.Teacher)]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var error = await payments.DeleteAsync(id);
        return error is null ? NoContent() : this.ToHttp(error);
    }

    /// <summary>
    /// שולחת תזכורת תשלום לכל הורה עם חיובים פתוחים (מרוכזת לפי ילד), לדייר הנוכחי בלבד.
    /// פועלת גם אוטומטית פעם בחודש לכל הדיירים דרך Hangfire (ראו MonthlyPaymentReminderJob).
    /// </summary>
    [Authorize(Roles = Roles.Teacher)]
    [HttpPost("send-monthly-reminders")]
    public async Task<ActionResult<object>> SendMonthlyReminders()
    {
        var sentCount = await monthlyReminderJob.RunForCurrentTenantAsync();
        return Ok(new { sentCount });
    }

    // ===== הורה (R3) =====

    [Authorize(Roles = Roles.Parent)]
    [HttpGet("mine/open-charges")]
    public async Task<ActionResult<IEnumerable<OpenChargeDto>>> MyOpenCharges() =>
        OkOr(await queries.OpenChargesForParentAsync(CurrentUserId));

    [Authorize(Roles = Roles.Parent)]
    [HttpGet("mine")]
    public async Task<ActionResult<IEnumerable<PaymentDto>>> MyPayments() =>
        OkOr(await queries.PaymentsOfParentAsync(CurrentUserId));

    private ActionResult OkOr<T>(ServiceResult<T> result) =>
        result.Error is null ? Ok(result.Value) : this.ToHttp(result.Error);
}
