using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace GridOps.Api.Auth;

// "Jwt" section. SigningKey comes from user secrets / App Service, never appsettings
public class JwtOptions
{
    public const string Section = "Jwt";

    public string Issuer { get; init; } = "";
    public string Audience { get; init; } = "";
    public string SigningKey { get; init; } = "";
    public int ExpiryMinutes { get; init; } = 120;

    public SymmetricSecurityKey GetSigningKey() => new(Encoding.UTF8.GetBytes(SigningKey));
}
