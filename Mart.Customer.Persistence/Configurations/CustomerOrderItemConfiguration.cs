using Mart.Customer.Domain.Orders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mart.Customer.Persistence.Configurations;

public sealed class CustomerOrderItemConfiguration : IEntityTypeConfiguration<CustomerOrderItem>
{
    public void Configure(EntityTypeBuilder<CustomerOrderItem> builder)
    {
        builder.ToTable("CustomerOrderItem", "customer");
        builder.HasKey(item => item.CustomerOrderItemId);
        builder.Ignore(item => item.CustomerCartItemId);
        builder.Property(item => item.ProductCodeSnapshot).HasMaxLength(50).IsUnicode(false);
        builder.Property(item => item.ProductNameSnapshot).HasMaxLength(500).IsRequired();
        builder.Property(item => item.HSNCodeSnapshot).HasMaxLength(20).IsUnicode(false);
        builder.Property(item => item.Quantity).HasPrecision(18, 3);
        builder.Property(item => item.UnitPrice).HasPrecision(18, 2);
        builder.Property(item => item.MRP).HasPrecision(18, 2);
        builder.Ignore(item => item.GrossAmount);
        builder.Property(item => item.DiscountAmount).HasPrecision(18, 2);
        builder.Property(item => item.TaxableAmount).HasPrecision(18, 2);
        builder.Property(item => item.CGSTAmount).HasPrecision(18, 2);
        builder.Property(item => item.SGSTAmount).HasPrecision(18, 2);
        builder.Property(item => item.IGSTAmount).HasPrecision(18, 2);
        builder.Property(item => item.GSTPercent).HasPrecision(5, 2);
        builder.Property(item => item.GSTAmount).HasPrecision(18, 2);
        builder.Property(item => item.LineTotal).HasPrecision(18, 2);
    }
}
