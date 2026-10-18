using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Talmidon.Api.Controllers;

/// <summary>
/// סימן חיים לשרת. קיים כדי שאפשר יהיה לדפוק על הדלת בלי להעיר את כל הבית.
///
/// בתוכנית החינמית של Render השרת נרדם אחרי כמה דקות בלי תנועה, והמבקר הבא ממתין
/// עד דקה — כלומר דווקא התלמידה המתעניינת, שמגיעה לספרייה הציבורית ולטופס הפנייה
/// ראשונה, פוגשת אתר שנראה מת. משימה מתוזמנת ב-GitHub Actions מושכת את הנתיב הזה
/// בשעות שבהן אנשים ערים (ראו .github/workflows/keep-awake.yml).
///
/// חשוב מאוד: אין כאן פנייה לבסיס הנתונים, ואסור שתהיה. גם Neon בתוכנית החינמית
/// נרדם לבד, ויש לו מכסת שעות חישוב חודשית — שאילתה אחת כל עשר דקות הייתה מחזיקה
/// גם אותו ער מסביב לשעון ושורפת את המכסה בלי שום תועלת. הבדיקה
/// HealthEndpointTests מוודאת שהתלות הזו לא תיכנס לכאן בטעות בעתיד.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/health")]
public class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(new { status = "ok", utc = DateTimeOffset.UtcNow });
}
