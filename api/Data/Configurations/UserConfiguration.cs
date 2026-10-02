using GridOps.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GridOps.Api.Data.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.Property(u => u.Email).HasMaxLength(256);
        builder.Property(u => u.DisplayName).HasMaxLength(100);
        builder.Property(u => u.PasswordHash).HasMaxLength(200);

        // used for login lookup + no duplicate accounts
        builder.HasIndex(u => u.Email).IsUnique();
    }
}
