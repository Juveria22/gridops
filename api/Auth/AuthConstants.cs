using GridOps.Api.Domain;

namespace GridOps.Api.Auth;

// for [Authorize(Roles = ...)] - attributes need compile-time constants
public static class Roles
{
    public const string Dispatcher = nameof(UserRole.Dispatcher);
    public const string Crew = nameof(UserRole.Crew);
}

// claim names inside the token. short, standard where one exists
public static class ClaimNames
{
    public const string Subject = "sub";
    public const string Email = "email";
    public const string Name = "name";
    public const string Role = "role";
    public const string CrewId = "crew_id";
}

public static class RateLimits
{
    public const string Login = "login";
}
