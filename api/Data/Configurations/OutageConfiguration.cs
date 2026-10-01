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

        // deleting an outage deletes its work orders
        builder.HasMany(o => o.WorkOrders)
            .WithOne(w => w.Outage)
            .HasForeignKey(w => w.OutageId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
