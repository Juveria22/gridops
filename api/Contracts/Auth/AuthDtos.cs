using System.ComponentModel.DataAnnotations;
using GridOps.Api.Domain;

namespace GridOps.Api.Contracts.Auth;

public class LoginRequest
{
    [Required, EmailAddress, MaxLength(256)]
    public string Email { get; init; } = "";

    [Required, MaxLength(200)]
    public string Password { get; init; } = "";
}

public record CurrentUserDto(int Id, string Email, string DisplayName, UserRole Role, int? CrewId, string? CrewName);

public record LoginResponse(string AccessToken, DateTimeOffset ExpiresAt, CurrentUserDto User);
