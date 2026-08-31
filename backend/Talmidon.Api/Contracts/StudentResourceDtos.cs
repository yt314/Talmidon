using System.ComponentModel.DataAnnotations;

namespace Talmidon.Api.Contracts;

public record CreateStudentResourceRequest(
    [Required, MaxLength(200)] string Title,
    [Required, Url, MaxLength(2000)] string Url,
    [MaxLength(1000)] string? Description);

public record StudentResourceDto(
    Guid Id,
    Guid StudentId,
    string Title,
    string Url,
    string? Description,
    DateTimeOffset CreatedAt);
