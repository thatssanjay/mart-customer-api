using Mart.Customer.Domain.Carts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mart.Customer.Persistence.Configurations;

public sealed class CustomerCartConfiguration : IEntityTypeConfiguration<CustomerCart>
{
    public void Configure(EntityTypeBuilder<CustomerCart> builder)
    {
        builder.ToTable("CustomerCart", "customer");
        builder.HasKey(cart => cart.CustomerCartId);
        builder.Property(cart => cart.CartNumber).HasMaxLength(50).IsRequired();
        builder.HasIndex(cart => cart.CartNumber).IsUnique();
        builder.Property(cart => cart.CartStatus).HasMaxLength(30).IsRequired();
        builder.Property(cart => cart.GrossAmount).HasPrecision(18, 2);
        builder.Property(cart => cart.DiscountAmount).HasPrecision(18, 2);
        builder.Property(cart => cart.GSTAmount).HasPrecision(18, 2);
        builder.Property(cart => cart.NetAmount).HasPrecision(18, 2);
        builder.Property(cart => cart.RewardPointsToRedeem).HasPrecision(18, 2);
        builder.Property(cart => cart.RedeemAmount).HasPrecision(18, 2);
        builder.Property(cart => cart.FinalPayableAmount).HasPrecision(18, 2);
        builder.Property(cart => cart.Remarks).HasMaxLength(500);

        builder.HasMany(cart => cart.Items)
            .WithOne(item => item.CustomerCart)
            .HasForeignKey(item => item.CustomerCartId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(cart => cart.Items)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
