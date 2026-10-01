using System.ComponentModel.DataAnnotations;
using GridOps.Api.Domain;

namespace GridOps.Api.Contracts.Outages;

// no Id/Status/timestamps here - server sets those (stops over-posting)
public class CreateOutageRequest : IValidatableObject
{
    [Required, MaxLength(200)]
    public string Title { get; init; } = "";

    [MaxLength(2000)]
    public string? Description { get; init; }

    // nullable + Required: missing value is a 400, not silently the first enum value
    [Required]
    public Borough? Borough { get; init; }

    [Required, MaxLength(100)]
    public string Neighborhood { get; init; } = "";

    [Range(0, 10_000_000)]
    public int CustomersAffected { get; init; }

    [Required]
    public Priority? Priority { get; init; }

    // defaults to now. allows logging an outage reported earlier by phone
    public DateTimeOffset? ReportedAt { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (ReportedAt > DateTimeOffset.UtcNow.AddMinutes(5))
            yield return new ValidationResult("Cannot be in the future.", [nameof(ReportedAt)]);
    }
}

public class UpdateOutageStatusRequest
{
    [Required]
    public OutageStatus? Status { get; init; }
}
