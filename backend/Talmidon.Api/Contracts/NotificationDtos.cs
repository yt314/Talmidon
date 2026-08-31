using Talmidon.Domain.Enums;

namespace Talmidon.Api.Contracts;

public record NotificationDto(
    Guid Id,
    NotificationType Type,
    string Title,
    string Message,
    string? LinkPath,
    bool IsRead,
    DateTimeOffset CreatedAt);

public record UnreadCountDto(int Count);
