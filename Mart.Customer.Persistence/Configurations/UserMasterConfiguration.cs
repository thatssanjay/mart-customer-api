using Mart.Customer.Persistence.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mart.Customer.Persistence.Configurations;

public sealed class UserMasterConfiguration : IEntityTypeConfiguration<UserMasterEntity>
{
    public void Configure(EntityTypeBuilder<UserMasterEntity> builder)
    {
        builder.ToTable("usermaster", "mart");

        builder.HasKey(user => user.UserId);

        builder.Property(user => user.UserCode).HasMaxLength(50);
        builder.Property(user => user.FirstName).HasMaxLength(100);
        builder.Property(user => user.MiddleName).HasMaxLength(100);
        builder.Property(user => user.LastName).HasMaxLength(100);
        builder.Property(user => user.DisplayName).HasMaxLength(150);
        builder.Property(user => user.Email).HasMaxLength(256);
        builder.Property(user => user.MobileNumber).HasMaxLength(20);
        builder.Property(user => user.Username).HasMaxLength(100);
        builder.Property(user => user.PasswordHash).HasMaxLength(256);
        builder.Property(user => user.PasswordSalt).HasMaxLength(128);
        builder.Property(user => user.ProfileImageUrl).HasMaxLength(500);
        builder.Property(user => user.PasswordResetTokenHash).HasMaxLength(128);

        builder.HasMany(user => user.UserRoles)
            .WithOne(userRole => userRole.User)
            .HasForeignKey(userRole => userRole.UserId);
    }
}
