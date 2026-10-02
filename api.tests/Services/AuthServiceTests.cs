using GridOps.Api.Auth;
using GridOps.Api.Common.Errors;
using GridOps.Api.Contracts.Auth;
using GridOps.Api.Data;
using GridOps.Api.Domain;
using GridOps.Api.Services;
using GridOps.Api.Tests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using static GridOps.Api.Tests.Infrastructure.TestData;

namespace GridOps.Api.Tests.Services;

public class AuthServiceTests(SqlServerFixture fixture) : DatabaseTest(fixture)
{
    private const string Password = "Correct-Horse-9!";
    private readonly PasswordHasher<User> _hasher = new();

    private AuthService Service(GridOpsDbContext db)
    {
        var jwt = Options.Create(new JwtOptions
        {
            Issuer = "test-issuer",
            Audience = "test-audience",
            SigningKey = "test-signing-key-that-is-at-least-32-bytes-long",
            ExpiryMinutes = 120,
        });
        return new AuthService(db, _hasher, new TokenService(jwt, Clock));
    }

    private User UserWithPassword(string email, UserRole role, Crew? crew = null)
    {
        var user = new User { Email = email, DisplayName = "Test User", Role = role, Crew = crew };
        user.PasswordHash = _hasher.HashPassword(user, Password);
        return user;
    }

    [Fact]
    public async Task Valid_login_returns_token_with_role_and_crew_claims()
    {
        var crew = Crew();
        var user = UserWithPassword("crew@test.com", UserRole.Crew, crew);
        await SeedAsync(user);

        await using var db = NewDb();
        var response = await Service(db).LoginAsync(new LoginRequest { Email = "crew@test.com", Password = Password }, default);

        var token = new JsonWebTokenHandler().ReadJsonWebToken(response.AccessToken);
        Assert.Equal(user.Id.ToString(), token.Subject);
        Assert.Equal("Crew", token.GetClaim(ClaimNames.Role).Value);
        Assert.Equal(crew.Id.ToString(), token.GetClaim(ClaimNames.CrewId).Value);
        Assert.Equal(Now.AddMinutes(120), response.ExpiresAt);
        Assert.Equal(crew.Name, response.User.CrewName);
    }

    [Fact]
    public async Task Email_is_matched_case_insensitively_and_trimmed()
    {
        await SeedAsync(UserWithPassword("dispatcher@test.com", UserRole.Dispatcher));

        await using var db = NewDb();
        var response = await Service(db).LoginAsync(
            new LoginRequest { Email = "  Dispatcher@TEST.com ", Password = Password }, default);

        Assert.Equal("dispatcher@test.com", response.User.Email);
    }

    [Fact]
    public async Task Wrong_password_and_unknown_email_get_the_same_error()
    {
        // different messages would let attackers find which emails have accounts
        await SeedAsync(UserWithPassword("dispatcher@test.com", UserRole.Dispatcher));

        await using var db = NewDb();
        var wrongPassword = await Assert.ThrowsAsync<UnauthorizedException>(() => Service(db)
            .LoginAsync(new LoginRequest { Email = "dispatcher@test.com", Password = "nope" }, default));
        var unknownEmail = await Assert.ThrowsAsync<UnauthorizedException>(() => Service(db)
            .LoginAsync(new LoginRequest { Email = "nobody@test.com", Password = "nope" }, default));

        Assert.Equal(wrongPassword.Message, unknownEmail.Message);
    }

    [Fact]
    public async Task User_without_a_password_hash_cannot_log_in()
    {
        await SeedAsync(new User { Email = "nopass@test.com", DisplayName = "No Pass", Role = UserRole.Crew });

        await using var db = NewDb();
        await Assert.ThrowsAsync<UnauthorizedException>(() => Service(db)
            .LoginAsync(new LoginRequest { Email = "nopass@test.com", Password = "" }, default));
    }

    [Fact]
    public void Same_password_gets_a_different_hash_per_user()
    {
        var a = UserWithPassword("a@test.com", UserRole.Crew);
        var b = UserWithPassword("b@test.com", UserRole.Crew);

        Assert.NotEqual(a.PasswordHash, b.PasswordHash); // random salt
        Assert.DoesNotContain(Password, a.PasswordHash);
    }
}
