using GridOps.Api.Domain;

namespace GridOps.Api.Contracts.WorkOrders;

// outage title/location included - crew can't call the outage endpoints
public record WorkOrderDto(
    int Id,
    int OutageId,
    string OutageTitle,
    Borough Borough,
    string Neighborhood,
    string Title,
    string? Notes,
    WorkOrderStatus Status,
    Priority Priority,
    int? CrewId,
    string? CrewName,
    DateTimeOffset? CompletedAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
