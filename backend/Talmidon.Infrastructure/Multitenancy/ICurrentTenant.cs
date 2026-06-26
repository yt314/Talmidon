namespace Talmidon.Infrastructure.Multitenancy;

/// <summary>
/// מספק את מזהה הדייר (המורה) של הבקשה הנוכחית.
/// המימוש האמיתי (שלב 5.3) קורא את ה-TenantId מתוך תביעות (claims) של טוקן ה-JWT.
/// </summary>
public interface ICurrentTenant
{
    Guid? TenantId { get; }
}

/// <summary>
/// מימוש ברירת מחדל — ללא דייר (null). משמש בזמן-תכן (migrations) ובהקשרים אנונימיים.
/// כש-TenantId הוא null, מסנן הדייר אינו מחזיר אף ישות מסוננת — ברירת מחדל בטוחה.
/// </summary>
public sealed class NullCurrentTenant : ICurrentTenant
{
    public Guid? TenantId => null;
}
