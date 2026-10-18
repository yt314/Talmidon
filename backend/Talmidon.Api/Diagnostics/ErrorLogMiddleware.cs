using System.Security.Claims;
using System.Text.Json;
using Talmidon.Infrastructure.Diagnostics;
using Talmidon.Infrastructure.Multitenancy;

namespace Talmidon.Api.Diagnostics;

/// <summary>
/// תופס כל חריגה שלא טופלה, רושם אותה ביומן התקלות, ומחזיר ללקוח קוד קצר לציטוט.
///
/// עד כה חריגה כזו הגיעה לדפדפן כ-500 ריק: המשתמשת ראתה "הפעולה נכשלה" ולא נשאר שום
/// חוט למשוך בו. הקוד שחוזר כאן הוא אותו קוד שמופיע במסך התקלות בניהול, וזה כל ההבדל
/// בין "משהו לא עבד לי אתמול" לבין שורה אחת עם עקבת מחסנית.
///
/// נרשם כרכיב ביניים ולא כ-<c>UseExceptionHandler</c> במכוון: בסביבת פיתוח ASP.NET
/// מוסיפה את דף החריגות שלה בראש הצינור, והוא היה בולע את החריגה לפני כל מטפל שנרשם
/// אחריו — כלומר גם הבדיקות, שרצות כ-Development, לא היו מגיעות לכאן.
/// </summary>
public sealed class ErrorLogMiddleware(RequestDelegate next, ILogger<ErrorLogMiddleware> logger)
{
    private const string FallbackMessage = "אירעה תקלה בשרת. נסי שוב, ואם זה חוזר — מסרי את קוד התקלה.";

    /// <summary>אותו סגנון שמות ששאר ה-API מחזיר (camelCase).</summary>
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task InvokeAsync(HttpContext context, IErrorLog errorLog, ICurrentTenant currentTenant)
    {
        try
        {
            await next(context);
        }
        catch (Exception exception)
        {
            // משתמשת שסגרה לשונית באמצע בקשה אינה תקלה. בלי הענף הזה כל ניווט מהיר
            // היה מוסיף שורה ליומן, והתקלות האמיתיות היו נקברות תחתיהן.
            if (context.RequestAborted.IsCancellationRequested && exception is OperationCanceledException)
            {
                logger.LogDebug("Request to {Path} was aborted by the client.", context.Request.Path.Value);
                return;
            }

            var reference = await errorLog.RecordAsync(exception, new ErrorOrigin
            {
                Source = "request",
                Path = context.Request.Path.Value,
                Method = context.Request.Method,
                UserId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value,
                UserEmail = context.User.FindFirst(ClaimTypes.Email)?.Value
                    ?? context.User.FindFirst("email")?.Value,
                TenantId = currentTenant.TenantId
            });

            // אם כבר התחלנו לשלוח תשובה אי אפשר להחליף אותה בשגיאה; לפחות היא נרשמה.
            if (context.Response.HasStarted)
            {
                logger.LogWarning("Error {Reference} occurred after the response had started.", reference);
                return;
            }

            // מה שהפעולה שנכשלה הספיקה להכריז על התשובה כבר לא נכון: בקשת הורדה,
            // למשל, הספיקה לקבוע שם קובץ. שאר הכותרות (CORS, HSTS) נשארות.
            context.Response.ContentLength = null;
            context.Response.Headers.Remove("Content-Disposition");
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/json; charset=utf-8";

            // אותו גוף שכל שאר השגיאות מחזירות ({ message }), ועוד שדה אחד — כך הלקוח
            // קורא את ההודעה בדיוק כמו תמיד, ומצרף את הקוד במקום אחד ולא בכל מסך.
            var body = JsonSerializer.Serialize(new { message = FallbackMessage, reference }, JsonOptions);
            await context.Response.WriteAsync(body);
        }
    }
}

public static class ErrorLogMiddlewareExtensions
{
    public static IApplicationBuilder UseErrorLog(this IApplicationBuilder app) =>
        app.UseMiddleware<ErrorLogMiddleware>();
}
