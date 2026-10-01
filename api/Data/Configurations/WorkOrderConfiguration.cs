using GridOps.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GridOps.Api.Data.Configurations;

public class WorkOrderConfiguration : IEntityTypeConfiguration<WorkOrder>
{
    public void Configure(EntityTypeBuilder<WorkOrder> builder)
    {
        builder.Property(w => w.Title).HasMaxLength(200);
        builder.Property(w => w.Notes).HasMaxLength(2000);

        // deleting a crew unassigns its work orders instead of deleting them
        builder.HasOne(w => w.Crew)
            .WithMany(c => c.WorkOrders)
            .HasForeignKey(w => w.CrewId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
