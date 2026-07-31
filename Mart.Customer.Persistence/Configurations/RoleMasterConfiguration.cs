using Mart.Customer.Persistence.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mart.Customer.Persistence.Configurations;

public sealed class RoleMasterConfiguration : IEntityTypeConfiguration<RoleMasterEntity>
{
    public void Configure(EntityTypeBuilder<RoleMasterEntity> builder)
    {
        builder.ToTable("RoleMaster", "mart");

        builder.HasKey(role => role.RoleId);

        builder.Property(role => role.RoleCode).HasMaxLength(50);
        builder.Property(role => role.RoleName).HasMaxLength(100);
    }
}
