namespace GridOps.Api.Domain;

// set by GridOpsDbContext on save
public interface ITimestamped
{
    DateTimeOffset CreatedAt { get; set; }
    DateTimeOffset UpdatedAt { get; set; }
}
