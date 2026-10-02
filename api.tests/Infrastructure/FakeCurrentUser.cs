using GridOps.Api.Auth;
using GridOps.Api.Domain;

namespace GridOps.Api.Tests.Infrastructure;

// stands in for the JWT claims - no login needed in service tests
public sealed class FakeCurrentUser(UserRole role, int? crewId = null, int id = 1) : ICurrentUser
{
    public int Id => id;
    public UserRole Role => role;
    public int? CrewId => crewId;

    public static FakeCurrentUser Dispatcher() => new(UserRole.Dispatcher);
    public static FakeCurrentUser CrewMember(int? crewId) => new(UserRole.Crew, crewId, id: 2);
}
