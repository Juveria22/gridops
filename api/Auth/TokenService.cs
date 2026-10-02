using GridOps.Api.Domain;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace GridOps.Api.Auth;

public class TokenService(IOptions<JwtOptions> options, TimeProvider clock)
{
    public (string Token, DateTimeOffset ExpiresAt) Create(User user)
    {
        var jwt = options.Value;
        var now = clock.GetUtcNow();
        var expiresAt = now.AddMinutes(jwt.ExpiryMinutes);

        // payload is readable by anyone (base64) - facts only, no secrets
        var claims = new Dictionary<string, object>
        {
            [ClaimNames.Subject] = user.Id.ToString(),
            [ClaimNames.Email] = user.Email,
            [ClaimNames.Name] = user.DisplayName,
            [ClaimNames.Role] = user.Role.ToString(),
        };
        if (user.CrewId is not null)
            claims[ClaimNames.CrewId] = user.CrewId.Value.ToString();

        var token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = jwt.Issuer,
            Audience = jwt.Audience,
            Claims = claims,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            SigningCredentials = new SigningCredentials(jwt.GetSigningKey(), SecurityAlgorithms.HmacSha256),
        });

        return (token, expiresAt);
    }
}
