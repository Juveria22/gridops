using GridOps.Api.Auth;
using GridOps.Api.Common.Errors;
using GridOps.Api.Contracts.Auth;
using GridOps.Api.Data;
using GridOps.Api.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace GridOps.Api.Services;

public interface IAuthService
{
    Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken ct);
    Task<CurrentUserDto> GetUserAsync(int id, CancellationToken ct);
}

public class AuthService(GridOpsDbContext db, IPasswordHasher<User> hasher, TokenService tokens) : IAuthService
{
    // used when the email doesn't exist so the response takes the same time
    private static readonly User DummyUser = new() { Email = "", DisplayName = "" };
    private static readonly Lazy<string> DummyHash = new(() => new PasswordHasher<User>().HashPassword(DummyUser, "dummy"));

    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await db.Users.Include(u => u.Crew).FirstOrDefaultAsync(u => u.Email == email, ct);

        if (user is null)
        {
            hasher.VerifyHashedPassword(DummyUser, DummyHash.Value, request.Password);
            throw InvalidLogin();
        }

        var result = hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (result == PasswordVerificationResult.Failed)
            throw InvalidLogin();

        // hash made with older settings -> upgrade it now that we have the password
        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = hasher.HashPassword(user, request.Password);
            await db.SaveChangesAsync(ct);
        }

        var (token, expiresAt) = tokens.Create(user);
        return new LoginResponse(token, expiresAt, ToDto(user));
    }

    public async Task<CurrentUserDto> GetUserAsync(int id, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().Include(u => u.Crew).FirstOrDefaultAsync(u => u.Id == id, ct)
            ?? throw new UnauthorizedException("User no longer exists.");
        return ToDto(user);
    }

    // same message for unknown email and wrong password - no account enumeration
    private static UnauthorizedException InvalidLogin() => new("Invalid email or password.");

    private static CurrentUserDto ToDto(User u) =>
        new(u.Id, u.Email, u.DisplayName, u.Role, u.CrewId, u.Crew?.Name);
}
