using Mart.Customer.Persistence.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mart.Customer.Persistence.Configurations;

public sealed class UserMartAccessConfiguration : IEntityTypeConfiguration<UserMartAccessEntity>
{
    public void Configure(EntityTypeBuilder<UserMartAccessEntity> builder)
    {
        builder.ToTable("UserMartAccess", "mart");

        builder.HasKey(access => access.UserMartAccessId);
    }
}
