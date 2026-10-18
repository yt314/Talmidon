using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Talmidon.Tests;

/// <summary>
/// נקודת קצה שנופלת בכוונה, כדי לבדוק את יומן התקלות מקצה לקצה — דרך הצינור האמיתי
/// (רכיב הביניים, כתיבה למסד, התשובה שחוזרת ללקוח) ולא מול מוק.
///
/// יושבת בפרויקט הבדיקות בלבד ונטענת ל-API רק דרך <c>AddApplicationPart</c> של מארח
/// הבדיקות: אין שום נתיב שבו היא מגיעה לאפליקציה שרצה אצל משתמשות.
///
/// <c>AllowAnonymous</c> אינו מונע מהבדיקה לשלוח טוקן: האימות רץ על כל בקשה, ולכן
/// זהות המשתמשת נרשמת גם כאן — וזה בדיוק מה שצריך לבדוק.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/test-only")]
public class ThrowingTestController : ControllerBase
{
    /// <summary>ה-<paramref name="marker"/> נכנס גם לנתיב וגם להודעה — כך לכל בדיקה יש תקלה משלה.</summary>
    [HttpGet("throw/{marker}")]
    public IActionResult Throw(string marker) =>
        throw new InvalidOperationException($"בדיקה: תקלה מכוונת {marker}");

    /// <summary>תקלה שנושאת סוד בהודעה — כדי לוודא שהוא אינו נשמר כפי שהוא.</summary>
    [HttpGet("throw-with-secret/{marker}")]
    public IActionResult ThrowWithSecret(string marker) =>
        throw new InvalidOperationException($"בדיקה {marker}: Password=hunter2 ApiKey=sk-live-abcdef123456");
}
