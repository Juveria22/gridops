using System.Linq.Expressions;
using GridOps.Api.Contracts.Outages;
using GridOps.Api.Contracts.WorkOrders;
using GridOps.Api.Domain;

namespace GridOps.Api.Services;

// entity -> DTO as expressions so EF turns them into SQL (only selected columns fetched)
public static class Projections
{
    public static readonly Expression<Func<WorkOrder, WorkOrderDto>> WorkOrder = w => new WorkOrderDto(
        w.Id, w.OutageId, w.Title, w.Notes, w.Status, w.Priority,
        w.CrewId, w.Crew != null ? w.Crew.Name : null,
        w.CompletedAt, w.CreatedAt, w.UpdatedAt);

    public static readonly Expression<Func<Outage, OutageSummaryDto>> OutageSummary = o => new OutageSummaryDto(
        o.Id, o.Title, o.Borough, o.Neighborhood, o.Status, o.Priority, o.CustomersAffected,
        o.ReportedAt, o.ResolvedAt,
        o.WorkOrders.Count(w => w.Status != WorkOrderStatus.Completed && w.Status != WorkOrderStatus.Cancelled));
}
