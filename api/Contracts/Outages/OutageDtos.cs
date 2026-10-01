using GridOps.Api.Contracts.WorkOrders;
using GridOps.Api.Domain;

namespace GridOps.Api.Contracts.Outages;

// dashboard row
public record OutageSummaryDto(
    int Id,
    string Title,
    Borough Borough,
    string Neighborhood,
    OutageStatus Status,
    Priority Priority,
    int CustomersAffected,
    DateTimeOffset ReportedAt,
    DateTimeOffset? ResolvedAt,
    int OpenWorkOrders);

public record OutageDetailDto(
    int Id,
    string Title,
    string? Description,
    Borough Borough,
    string Neighborhood,
    OutageStatus Status,
    Priority Priority,
    int CustomersAffected,
    DateTimeOffset ReportedAt,
    DateTimeOffset? ResolvedAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<WorkOrderDto> WorkOrders);
