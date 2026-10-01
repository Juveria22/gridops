using GridOps.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace GridOps.Api.Data;

public class GridOpsDbContext(DbContextOptions<GridOpsDbContext> options) : DbContext(options)
{
    public DbSet<Outage> Outages => Set<Outage>();
    public DbSet<WorkOrder> WorkOrders => Set<WorkOrder>();
    public DbSet<Crew> Crews => Set<Crew>();
    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(GridOpsDbContext).Assembly);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // enums as strings - readable rows, safe if enum order changes
        configurationBuilder.Properties<Borough>().HaveConversion<string>().HaveMaxLength(20);
        configurationBuilder.Properties<Priority>().HaveConversion<string>().HaveMaxLength(20);
        configurationBuilder.Properties<OutageStatus>().HaveConversion<string>().HaveMaxLength(20);
        configurationBuilder.Properties<WorkOrderStatus>().HaveConversion<string>().HaveMaxLength(20);
        configurationBuilder.Properties<UserRole>().HaveConversion<string>().HaveMaxLength(20);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SetTimestamps();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        SetTimestamps();
        return base.SaveChanges();
    }

    private void SetTimestamps()
    {
        var now = DateTimeOffset.UtcNow;

        foreach (var entry in ChangeTracker.Entries<ITimestamped>())
        {
            if (entry.State == EntityState.Added)
            {
                // keep explicit values (seed data)
                if (entry.Entity.CreatedAt == default) entry.Entity.CreatedAt = now;
                if (entry.Entity.UpdatedAt == default) entry.Entity.UpdatedAt = entry.Entity.CreatedAt;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = now;
            }
        }
    }
}
