using System.ComponentModel.DataAnnotations;

namespace Talmidon.Api.Contracts;

public record CreateParentRequest(
    [Required, MaxLength(200)] string FullName,
    [Required, EmailAddress, MaxLength(256)] string Email,
    [MaxLength(40)] string? Phone);

public record UpdateParentRequest(
    [Required, MaxLength(200)] string FullName,
    [MaxLength(40)] string? Phone);

public record ParentDto(
    Guid Id,
    string FullName,
    string Email,
    string? Phone,
    int StudentCount);
