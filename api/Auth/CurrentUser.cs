using System.Security.Claims;
using GridOps.Api.Common.Errors;
using GridOps.Api.Domain;

namespace GridOps.Api.Auth;

// who is calling. services use this instead of HttpContext so tests can fake it
public interface ICurrentUser
{
    int Id { get; }
    UserRole Role { get; }
    int? CrewId { get; }
    bool IsDispatcher => Role == UserRole.Dispatcher;
}

// reads claims from the validated token
public class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal Principal =>
        accessor.HttpContext?.User is { Identity.IsAuthenticated: true } user
            ? user
            : throw new UnauthorizedException("Not signed in.");

    public int Id => int.Parse(Principal.FindFirstValue(ClaimNames.Subject)!);

    public UserRole Role => Enum.Parse<UserRole>(Principal.FindFirstValue(ClaimNames.Role)!);

    public int? CrewId => int.TryParse(Principal.FindFirstValue(ClaimNames.CrewId), out var id) ? id : null;
}
