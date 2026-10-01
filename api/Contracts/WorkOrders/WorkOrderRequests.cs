using System.ComponentModel.DataAnnotations;
using GridOps.Api.Common.Paging;
using GridOps.Api.Domain;

namespace GridOps.Api.Contracts.WorkOrders;

public class CreateWorkOrderRequest
{
    [Required, MaxLength(200)]
    public string Title { get; init; } = "";

    [MaxLength(2000)]
    public string? Notes { get; init; }

    // defaults to the outage's priority
    public Priority? Priority { get; init; }

    // optional - can assign later
    public int? CrewId { get; init; }
}

public class AssignCrewRequest
{
    // null = unassign
    public int? CrewId { get; init; }
}

public class UpdateWorkOrderStatusRequest
{
    [Required]
    public WorkOrderStatus? Status { get; init; }
}

// ?status=Assigned&crewId=4&outageId=12&page=1
public class WorkOrderQuery : PageQuery
{
    public WorkOrderStatus[]? Status { get; init; }
    public int? CrewId { get; init; }
    public int? OutageId { get; init; }
}
