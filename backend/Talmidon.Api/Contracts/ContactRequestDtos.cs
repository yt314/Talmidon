using System.ComponentModel.DataAnnotations;

namespace Talmidon.Api.Contracts;

/// <summary>פנייה מהספרייה הציבורית. נשלחת ללא התחברות.</summary>
public record CreateContactRequestRequest(
    [Required, MaxLength(200)] string FullName,
    [Required, MaxLength(40)] string Phone,
    [EmailAddress, MaxLength(256)] string? Email,
    [MaxLength(100)] string? Subject,
    [Required, MaxLength(2000)] string Message);

public record ContactRequestDto(
    Guid Id,
    string FullName,
    string Phone,
    string? Email,
    string? Subject,
    string Message,
    int Status,
    DateTimeOffset CreatedAt);

public record UpdateContactRequestStatusRequest([Range(0, 2)] int Status);
