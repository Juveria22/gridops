using GridOps.Api.Domain;

namespace GridOps.Api.Contracts.WorkOrders;

public record WorkOrderDto(
    int Id,
    int OutageId,
    string Title,
    string? Notes,
    WorkOrderStatus Status,
    Priority Priority,
    int? CrewId,
    string? CrewName,
    DateTimeOffset? CompletedAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
