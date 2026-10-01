using System.ComponentModel.DataAnnotations;
using GridOps.Api.Common.Paging;
using GridOps.Api.Domain;

namespace GridOps.Api.Contracts.Outages;

// whitelist - can't sort by arbitrary columns
public enum OutageSortField
{
    ReportedAt,
    Priority,
    CustomersAffected
}

// ?status=Reported&status=Restoring&borough=Brooklyn&from=2026-09-01&sortBy=priority&page=2
public class OutageQuery : PageQuery, IValidatableObject
{
    public OutageStatus[]? Status { get; init; }
    public Priority[]? Priority { get; init; }
    public Borough[]? Borough { get; init; }

    public DateTimeOffset? From { get; init; }
    public DateTimeOffset? To { get; init; }

    public OutageSortField SortBy { get; init; } = OutageSortField.ReportedAt;

    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (From > To)
            yield return new ValidationResult("'from' must be before 'to'.", [nameof(From), nameof(To)]);
    }
}
