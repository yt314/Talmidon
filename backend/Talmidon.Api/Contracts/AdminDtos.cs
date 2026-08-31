namespace Talmidon.Api.Contracts;

public record AdminTeacherDto(
    Guid Id,
    string FullName,
    string Email,
    DateTimeOffset CreatedAt,
    bool IsPublic,
    int StudentCount,
    bool IsLockedOut);
