using Microsoft.EntityFrameworkCore;

namespace GridOps.Api.Data;

public class GridOpsDbContext(DbContextOptions<GridOpsDbContext> options) : DbContext(options)
{
    // DbSets for Crew, Outage, WorkOrder, and User are added in Phase 2.
}
