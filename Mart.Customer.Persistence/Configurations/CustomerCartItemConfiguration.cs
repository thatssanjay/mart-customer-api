using Mart.Customer.Domain.Carts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mart.Customer.Persistence.Configurations;

public sealed class CustomerCartItemConfiguration : IEntityTypeConfiguration<CustomerCartItem>
{
    public void Configure(EntityTypeBuilder<CustomerCartItem> builder)
    {
        builder.ToTable("CustomerCartItem", "customer");
        builder.HasKey(item => item.CustomerCartItemId);
        builder.Property(item => item.ProductNameSnapshot).HasMaxLength(250).IsRequired();
        builder.Property(item => item.Quantity).HasPrecision(18, 3);
        builder.Property(item => item.UnitPrice).HasPrecision(18, 2);
        builder.Property(item => item.MRP).HasPrecision(18, 2);
        builder.Property(item => item.DiscountAmount).HasPrecision(18, 2);
        builder.Property(item => item.GSTPercent).HasPrecision(18, 2);
        builder.Property(item => item.GSTAmount).HasPrecision(18, 2);
        builder.Property(item => item.LineTotal).HasPrecision(18, 2);
        builder.Ignore(item => item.GrossAmount);
    }
}
