namespace GridOps.Api.Domain;

public class User : ITimestamped
{
    public int Id { get; set; }
    public required string Email { get; set; }
    public required string DisplayName { get; set; }

    // PBKDF2 hash + salt from PasswordHasher. never the plain password
    public string PasswordHash { get; set; } = "";

    public UserRole Role { get; set; }

    // only set for crew members
    public int? CrewId { get; set; }
    public Crew? Crew { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
