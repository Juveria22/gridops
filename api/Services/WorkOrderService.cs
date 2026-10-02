using GridOps.Api.Auth;
using GridOps.Api.Common.Errors;
using GridOps.Api.Common.Paging;
using GridOps.Api.Contracts.WorkOrders;
using GridOps.Api.Data;
using GridOps.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace GridOps.Api.Services;

public interface IWorkOrderService
{
    Task<PagedResult<WorkOrderDto>> ListAsync(WorkOrderQuery query, CancellationToken ct);
    Task<WorkOrderDto> GetAsync(int id, CancellationToken ct);
    Task<WorkOrderDto> CreateAsync(int outageId, CreateWorkOrderRequest request, CancellationToken ct);
    Task AssignCrewAsync(int id, int? crewId, CancellationToken ct);
    Task UpdateStatusAsync(int id, WorkOrderStatus status, CancellationToken ct);
}

public class WorkOrderService(GridOpsDbContext db, TimeProvider clock, ICurrentUser currentUser) : IWorkOrderService
{
    // every read/update goes through this. crew -> own crew's work only
    private IQueryable<WorkOrder> Visible()
    {
        if (currentUser.IsDispatcher) return db.WorkOrders;

        // crew user not on a crew sees nothing (not the unassigned ones)
        var crewId = currentUser.CrewId;
        return crewId is null ? db.WorkOrders.Where(_ => false) : db.WorkOrders.Where(w => w.CrewId == crewId);
    }

    public async Task<PagedResult<WorkOrderDto>> ListAsync(WorkOrderQuery query, CancellationToken ct)
    {
        var workOrders = Visible().AsNoTracking();

        if (query.Status is { Length: > 0 })
            workOrders = workOrders.Where(w => query.Status.Contains(w.Status));
        if (query.CrewId is not null)
            workOrders = workOrders.Where(w => w.CrewId == query.CrewId);
        if (query.OutageId is not null)
            workOrders = workOrders.Where(w => w.OutageId == query.OutageId);

        var total = await workOrders.CountAsync(ct);

        var sorted = query.SortDir == SortDirection.Desc
            ? workOrders.OrderByDescending(w => w.CreatedAt).ThenByDescending(w => w.Id)
            : workOrders.OrderBy(w => w.CreatedAt).ThenBy(w => w.Id);

        var items = await sorted
            .Skip(query.Skip)
            .Take(query.PageSize)
            .Select(Projections.WorkOrder)
            .ToListAsync(ct);

        return new PagedResult<WorkOrderDto>(items, query.Page, query.PageSize, total);
    }

    public async Task<WorkOrderDto> GetAsync(int id, CancellationToken ct)
    {
        // other crew's work order -> 404, don't confirm it exists
        var workOrder = await Visible()
            .AsNoTracking()
            .Where(w => w.Id == id)
            .Select(Projections.WorkOrder)
            .FirstOrDefaultAsync(ct);

        return workOrder ?? throw new NotFoundException("Work order", id);
    }

    public async Task<WorkOrderDto> CreateAsync(int outageId, CreateWorkOrderRequest request, CancellationToken ct)
    {
        var outage = await db.Outages.AsNoTracking().FirstOrDefaultAsync(o => o.Id == outageId, ct)
            ?? throw new NotFoundException("Outage", outageId);

        if (outage.Status == OutageStatus.Resolved)
            throw new ConflictException($"Outage {outageId} is resolved. Can't add work orders.");

        if (request.CrewId is not null)
            await EnsureCrewCanTakeWork(request.CrewId.Value, ct);

        var workOrder = new WorkOrder
        {
            OutageId = outageId,
            Title = request.Title.Trim(),
            Notes = request.Notes?.Trim(),
            Priority = request.Priority ?? outage.Priority,
            CrewId = request.CrewId,
            Status = request.CrewId is null ? WorkOrderStatus.Open : WorkOrderStatus.Assigned,
        };

        db.WorkOrders.Add(workOrder);
        await db.SaveChangesAsync(ct);

        return await GetAsync(workOrder.Id, ct);
    }

    public async Task AssignCrewAsync(int id, int? crewId, CancellationToken ct)
    {
        var workOrder = await FindTracked(id, ct);
        EnsureNotFinished(workOrder);

        if (crewId is null)
        {
            if (workOrder.Status == WorkOrderStatus.InProgress)
                throw new ConflictException($"Work order {id} is in progress. Can't unassign the crew.");

            workOrder.CrewId = null;
            workOrder.Status = WorkOrderStatus.Open;
        }
        else
        {
            await EnsureCrewCanTakeWork(crewId.Value, ct);
            workOrder.CrewId = crewId;
            if (workOrder.Status == WorkOrderStatus.Open)
                workOrder.Status = WorkOrderStatus.Assigned;
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateStatusAsync(int id, WorkOrderStatus status, CancellationToken ct)
    {
        var workOrder = await FindTracked(id, ct);
        if (workOrder.Status == status) return;

        EnsureNotFinished(workOrder);

        if (!currentUser.IsDispatcher && status is not (WorkOrderStatus.InProgress or WorkOrderStatus.Completed))
            throw new ForbiddenException("Crew members can only start or complete work orders.");

        // Open/Assigned follow crew assignment - set via the crew endpoint
        if (status is WorkOrderStatus.Open or WorkOrderStatus.Assigned)
            throw new ConflictException($"Status '{status}' is set by assigning or unassigning a crew.");

        if ((status is WorkOrderStatus.InProgress or WorkOrderStatus.Completed) && workOrder.CrewId is null)
            throw new ConflictException($"Work order {id} has no crew. Assign one first.");

        workOrder.Status = status;
        if (status == WorkOrderStatus.Completed)
            workOrder.CompletedAt = clock.GetUtcNow();

        await db.SaveChangesAsync(ct);
    }

    private async Task<WorkOrder> FindTracked(int id, CancellationToken ct) =>
        await Visible().FirstOrDefaultAsync(w => w.Id == id, ct)
        ?? throw new NotFoundException("Work order", id);

    private static void EnsureNotFinished(WorkOrder workOrder)
    {
        if (workOrder.Status is WorkOrderStatus.Completed or WorkOrderStatus.Cancelled)
            throw new ConflictException($"Work order {workOrder.Id} is {workOrder.Status.ToString().ToLowerInvariant()}.");
    }

    // bad id in the body -> 400 on that field, not 404 for the whole request
    private async Task EnsureCrewCanTakeWork(int crewId, CancellationToken ct)
    {
        var crew = await db.Crews.AsNoTracking().FirstOrDefaultAsync(c => c.Id == crewId, ct);
        if (crew is null)
            throw new InvalidRequestException("crewId", $"Crew {crewId} not found.");
        if (!crew.IsActive)
            throw new InvalidRequestException("crewId", $"Crew {crewId} is inactive.");
    }
}
