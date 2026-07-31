using Mart.Customer.Persistence.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mart.Customer.Persistence.Configurations;

public sealed class UserRoleConfiguration : IEntityTypeConfiguration<UserRoleEntity>
{
    public void Configure(EntityTypeBuilder<UserRoleEntity> builder)
    {
        builder.ToTable("UserRole", "mart");

        builder.HasKey(userRole => userRole.UserRoleId);

        builder.HasOne(userRole => userRole.Role)
            .WithMany(role => role.UserRoles)
            .HasForeignKey(userRole => userRole.RoleId);
    }
}
