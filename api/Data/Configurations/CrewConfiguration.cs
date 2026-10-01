using GridOps.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GridOps.Api.Data.Configurations;

public class CrewConfiguration : IEntityTypeConfiguration<Crew>
{
    public void Configure(EntityTypeBuilder<Crew> builder)
    {
        builder.Property(c => c.Name).HasMaxLength(100);
        builder.HasIndex(c => c.Name).IsUnique();

        builder.HasMany(c => c.Members)
            .WithOne(u => u.Crew)
            .HasForeignKey(u => u.CrewId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
