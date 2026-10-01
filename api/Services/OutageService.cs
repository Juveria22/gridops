using GridOps.Api.Common.Errors;
using GridOps.Api.Common.Paging;
using GridOps.Api.Contracts.Outages;
using GridOps.Api.Data;
using GridOps.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace GridOps.Api.Services;

public interface IOutageService
{
    Task<PagedResult<OutageSummaryDto>> ListAsync(OutageQuery query, CancellationToken ct);
    Task<OutageDetailDto> GetAsync(int id, CancellationToken ct);
    Task<OutageDetailDto> CreateAsync(CreateOutageRequest request, CancellationToken ct);
    Task UpdateStatusAsync(int id, OutageStatus status, CancellationToken ct);
}

public class OutageService(GridOpsDbContext db, TimeProvider clock) : IOutageService
{
    public async Task<PagedResult<OutageSummaryDto>> ListAsync(OutageQuery query, CancellationToken ct)
    {
        // read only -> no change tracking
        var outages = db.Outages.AsNoTracking();

        // only add filters that were sent. EF combines them into one WHERE
        if (query.Status is { Length: > 0 })
            outages = outages.Where(o => query.Status.Contains(o.Status));
        if (query.Priority is { Length: > 0 })
            outages = outages.Where(o => query.Priority.Contains(o.Priority));
        if (query.Borough is { Length: > 0 })
            outages = outages.Where(o => query.Borough.Contains(o.Borough));
        if (query.From is not null)
            outages = outages.Where(o => o.ReportedAt >= query.From);
        if (query.To is not null)
            outages = outages.Where(o => o.ReportedAt <= query.To);

        var total = await outages.CountAsync(ct);

        var items = await Sort(outages, query)
            .Skip(query.Skip)
            .Take(query.PageSize)
            .Select(Projections.OutageSummary)
            .ToListAsync(ct);

        return new PagedResult<OutageSummaryDto>(items, query.Page, query.PageSize, total);
    }

    public async Task<OutageDetailDto> GetAsync(int id, CancellationToken ct)
    {
        var outage = await db.Outages
            .AsNoTracking()
            .Where(o => o.Id == id)
            .Select(o => new OutageDetailDto(
                o.Id, o.Title, o.Description, o.Borough, o.Neighborhood, o.Status, o.Priority,
                o.CustomersAffected, o.ReportedAt, o.ResolvedAt, o.CreatedAt, o.UpdatedAt,
                o.WorkOrders.AsQueryable().OrderBy(w => w.CreatedAt).Select(Projections.WorkOrder).ToList()))
            .FirstOrDefaultAsync(ct);

        return outage ?? throw new NotFoundException("Outage", id);
    }

    public async Task<OutageDetailDto> CreateAsync(CreateOutageRequest request, CancellationToken ct)
    {
        var outage = new Outage
        {
            Title = request.Title.Trim(),
            Description = request.Description?.Trim(),
            Borough = request.Borough!.Value, // [Required] already checked
            Neighborhood = request.Neighborhood.Trim(),
            CustomersAffected = request.CustomersAffected,
            Priority = request.Priority!.Value,
            Status = OutageStatus.Reported,
            ReportedAt = request.ReportedAt ?? clock.GetUtcNow(),
        };

        db.Outages.Add(outage);
        await db.SaveChangesAsync(ct);

        return await GetAsync(outage.Id, ct);
    }

    public async Task UpdateStatusAsync(int id, OutageStatus status, CancellationToken ct)
    {
        var outage = await db.Outages.FirstOrDefaultAsync(o => o.Id == id, ct)
            ?? throw new NotFoundException("Outage", id);

        if (outage.Status == status) return;

        if (outage.Status == OutageStatus.Resolved)
            throw new ConflictException($"Outage {id} is already resolved.");

        if (status == OutageStatus.Resolved)
        {
            var openWork = await db.WorkOrders.CountAsync(w => w.OutageId == id
                && w.Status != WorkOrderStatus.Completed && w.Status != WorkOrderStatus.Cancelled, ct);
            if (openWork > 0)
                throw new ConflictException($"Outage {id} has {openWork} open work order(s). Complete or cancel them first.");

            outage.ResolvedAt = clock.GetUtcNow();
        }

        outage.Status = status;
        await db.SaveChangesAsync(ct);
    }

    private static IQueryable<Outage> Sort(IQueryable<Outage> outages, OutageQuery query)
    {
        var desc = query.SortDir == SortDirection.Desc;

        var sorted = query.SortBy switch
        {
            // priority stored as string -> alphabetical sort would be wrong. map to rank
            OutageSortField.Priority => desc ? outages.OrderByDescending(PriorityRank) : outages.OrderBy(PriorityRank),
            OutageSortField.CustomersAffected => desc ? outages.OrderByDescending(o => o.CustomersAffected) : outages.OrderBy(o => o.CustomersAffected),
            _ => desc ? outages.OrderByDescending(o => o.ReportedAt) : outages.OrderBy(o => o.ReportedAt),
        };

        // tie breaker so paging is stable (same row can't show on two pages)
        return desc ? sorted.ThenByDescending(o => o.Id) : sorted.ThenBy(o => o.Id);
    }

    private static readonly System.Linq.Expressions.Expression<Func<Outage, int>> PriorityRank = o =>
        o.Priority == Priority.Critical ? 3 :
        o.Priority == Priority.High ? 2 :
        o.Priority == Priority.Medium ? 1 : 0;
}
