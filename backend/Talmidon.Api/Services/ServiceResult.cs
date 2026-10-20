using Microsoft.AspNetCore.Mvc;

namespace Talmidon.Api.Services;

public enum ServiceFailure { NotFound, Forbidden, Conflict, Invalid, Unauthorized }

/// <summary>
/// למה פעולת שירות לא בוצעה. השירות מחליט מה קרה; רק הבקר יודע איך זה נראה ב-HTTP
/// (ראו <see cref="ServiceErrorHttp"/>).
/// </summary>
public sealed record ServiceError(ServiceFailure Failure, string? Message = null)
{
    public static ServiceError NotFound(string? message = null) => new(ServiceFailure.NotFound, message);
    public static readonly ServiceError Forbidden = new(ServiceFailure.Forbidden);
    public static ServiceError Conflict(string message) => new(ServiceFailure.Conflict, message);
    public static ServiceError Invalid(string message) => new(ServiceFailure.Invalid, message);
    public static ServiceError Unauthorized(string message) => new(ServiceFailure.Unauthorized, message);
}

/// <summary>ערך או שגיאה. אפשר להחזיר מהשירות כל אחד מהם ישירות — ההמרה מרומזת.</summary>
public readonly record struct ServiceResult<T>(T? Value, ServiceError? Error)
{
    public static implicit operator ServiceResult<T>(T value) => new(value, null);
    public static implicit operator ServiceResult<T>(ServiceError error) => new(default, error);
}

public static class ServiceErrorHttp
{
    public static ActionResult ToHttp(this ControllerBase controller, ServiceError error) => error.Failure switch
    {
        ServiceFailure.NotFound => error.Message is null ? controller.NotFound() : controller.NotFound(new { message = error.Message }),
        ServiceFailure.Forbidden => controller.Forbid(),
        ServiceFailure.Conflict => controller.Conflict(new { message = error.Message }),
        ServiceFailure.Unauthorized => controller.Unauthorized(new { message = error.Message }),
        _ => controller.BadRequest(new { message = error.Message })
    };
}
