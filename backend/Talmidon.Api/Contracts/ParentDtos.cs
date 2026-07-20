using System.ComponentModel.DataAnnotations;
using Talmidon.Domain.Enums;

namespace Talmidon.Api.Contracts;

public record CreateParentRequest(
    [Required, MaxLength(200)] string FullName,
    Gender? Gender,
    [Required, EmailAddress, MaxLength(256)] string Email,
    [MaxLength(40)] string? Phone);

public record UpdateParentRequest(
    [Required, MaxLength(200)] string FullName,
    Gender? Gender,
    [MaxLength(40)] string? Phone);

public record ParentDto(
    Guid Id,
    string FullName,
    Gender? Gender,
    string Email,
    string? Phone,
    int StudentCount);

/// <summary>תצוגת הורה על עצמו — לפנייה מותאמת בממשק (R-self).</summary>
public record MyParentProfileDto(string FullName, Gender? Gender);
