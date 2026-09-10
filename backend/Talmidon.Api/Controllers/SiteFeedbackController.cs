using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Talmidon.Api.Contracts;
using Talmidon.Domain.Entities;
using Talmidon.Infrastructure.Data;
using Talmidon.Infrastructure.Email;

namespace Talmidon.Api.Controllers;

/// <summary>
/// הודעות מהאתר בתקופת ההרצה — רעיון או תקלה, מכל מבקר ובלי התחברות.
///
/// פתוח לאנונימיים ולכן מוגבל בקצב, כמו טופס הפנייה בספרייה. ההודעה נשמרת תמיד, וגם
/// נשלחת במייל למנהל אם הוגדרה כתובת; כשל בשליחת המייל אינו מפיל את הבקשה, כי ההודעה
/// כבר במסד ומי ששלח אותה לא אמור לראות שגיאה בגלל ספק מייל.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/public/feedback")]
public class SiteFeedbackController(
    TalmidonDbContext db,
    IEmailSender emailSender,
    IConfiguration configuration,
    AppLinks links,
    ILogger<SiteFeedbackController> logger) : ControllerBase
{
    [HttpPost]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Send(CreateSiteFeedbackRequest request)
    {
        var feedback = new SiteFeedback
        {
            Id = Guid.NewGuid(),
            Message = request.Message.Trim(),
            ContactInfo = string.IsNullOrWhiteSpace(request.ContactInfo) ? null : request.ContactInfo.Trim(),
            PageUrl = string.IsNullOrWhiteSpace(request.PageUrl) ? null : request.PageUrl.Trim(),
            IsHandled = false,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.SiteFeedback.Add(feedback);
        await db.SaveChangesAsync();

        await NotifyAdminAsync(feedback);
        return NoContent();
    }

    private async Task NotifyAdminAsync(SiteFeedback feedback)
    {
        var adminEmail = configuration["Admin:Email"] ?? Environment.GetEnvironmentVariable("ADMIN_EMAIL");
        if (string.IsNullOrWhiteSpace(adminEmail)) return;

        var body = EmailLayout.Render(new EmailMessage(
            "הודעה חדשה מהאתר",
            Quote: feedback.Message,
            Details:
            [
                ("ליצירת קשר", feedback.ContactInfo ?? ""),
                ("מהדף", feedback.PageUrl ?? "")
            ],
            ActionLabel: "לצפייה בפניות",
            ActionUrl: links.AdminFeedback));

        try
        {
            await emailSender.SendAsync(adminEmail, EmailSubjects.SiteFeedback(feedback.Message), body);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Site feedback {FeedbackId} was saved but the notification email failed.", feedback.Id);
        }
    }
}
