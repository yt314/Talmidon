namespace Talmidon.Domain.Common;

/// <summary>
/// מסמן ישות שבבעלות מורה (דייר). על כל ישות כזו EF Core מחיל אוטומטית
/// Global Query Filter: <c>WHERE TenantId = currentTenant</c>.
/// הערך של <see cref="TenantId"/> שווה תמיד ל-Id של המורה הבעלים.
/// </summary>
public interface ITenantScoped
{
    Guid TenantId { get; set; }
}
