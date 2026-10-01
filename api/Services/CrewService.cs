using GridOps.Api.Contracts.Crews;
using GridOps.Api.Data;
using GridOps.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace GridOps.Api.Services;

public interface ICrewService
{
    Task<IReadOnlyList<CrewDto>> ListAsync(CrewQuery query, CancellationToken ct);
}

public class CrewService(GridOpsDbContext db) : ICrewService
{
    // ~30 crews -> no paging needed
    public async Task<IReadOnlyList<CrewDto>> ListAsync(CrewQuery query, CancellationToken ct)
    {
        var crews = db.Crews.AsNoTracking();

        if (!query.IncludeInactive)
            crews = crews.Where(c => c.IsActive);
        if (query.Borough is not null)
            crews = crews.Where(c => c.HomeBorough == query.Borough);

        return await crews
            .OrderBy(c => c.Name)
            .Select(c => new CrewDto(
                c.Id, c.Name, c.HomeBorough, c.IsActive,
                c.Members.Count,
                c.WorkOrders.Count(w => w.Status == WorkOrderStatus.Assigned || w.Status == WorkOrderStatus.InProgress)))
            .ToListAsync(ct);
    }
}
