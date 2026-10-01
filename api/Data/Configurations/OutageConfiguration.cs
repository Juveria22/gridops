using GridOps.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GridOps.Api.Data.Configurations;

public class OutageConfiguration : IEntityTypeConfiguration<Outage>
{
    public void Configure(EntityTypeBuilder<Outage> builder)
    {
        builder.Property(o => o.Title).HasMaxLength(200);
        builder.Property(o => o.Description).HasMaxLength(2000);
        builder.Property(o => o.Neighborhood).HasMaxLength(100);

        // dashboard filters. equality column first, then ReportedAt for range + sort
        // active list: include Borough/Priority so filtering on them doesn't need table lookups
        builder.HasIndex(o => new { o.Status, o.ReportedAt })
            .IncludeProperties(o => new { o.Borough, o.Priority });
        builder.HasIndex(o => new { o.Priority, o.ReportedAt });
        builder.HasIndex(o => new { o.Borough, o.ReportedAt });
        // date range only + default newest-first sort
        builder.HasIndex(o => o.ReportedAt);

        // deleting an outage deletes its work orders
        builder.HasMany(o => o.WorkOrders)
            .WithOne(w => w.Outage)
            .HasForeignKey(w => w.OutageId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
