using System.ComponentModel.DataAnnotations;

namespace Talmidon.Api.Contracts;

public record RegisterRequest(
    [Required, EmailAddress, MaxLength(256)] string Email,
    [Required, MinLength(8), MaxLength(100)] string Password,
    [Required, MaxLength(200)] string FullName,
    [MaxLength(40)] string? Phone);

public record LoginRequest(
    [Required, EmailAddress] string Email,
    [Required] string Password);

public record RefreshRequest(
    [Required] string RefreshToken);

public record ResendConfirmationRequest(
    [Required, EmailAddress] string Email);

public record SetPasswordRequest(
    [Required] string UserId,
    [Required] string Token,
    [Required, MinLength(8), MaxLength(100)] string Password);

public record AuthResponse(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    string Email,
    string[] Roles);
