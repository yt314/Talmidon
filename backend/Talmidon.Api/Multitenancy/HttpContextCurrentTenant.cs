using Talmidon.Infrastructure.Auth;
using Talmidon.Infrastructure.Multitenancy;

namespace Talmidon.Api.Multitenancy;

/// <summary>
/// מימוש <see cref="ICurrentTenant"/> שקורא את מזהה הדייר מתביעת ה-"tenant" של טוקן ה-JWT.
/// בקשות אנונימיות / ללא התביעה → null (fail-closed: אפס שורות מסוננות).
/// </summary>
public class HttpContextCurrentTenant(IHttpContextAccessor accessor) : ICurrentTenant
{
    public Guid? TenantId
    {
        get
        {
            var value = accessor.HttpContext?.User?.FindFirst(ITokenService.TenantClaim)?.Value;
            return Guid.TryParse(value, out var id) ? id : null;
        }
    }
}
