using Mart.Customer.Domain.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mart.Customer.Persistence.Configurations;

public sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("productmaster", "inventory");

        builder.HasKey(product => product.ProductId);

        builder.Property(product => product.ProductCode)
            .HasMaxLength(50);

        builder.Property(product => product.ProductName)
            .HasMaxLength(250);

        builder.Property(product => product.GSTPercent)
            .HasPrecision(18, 2);

        builder.Property(product => product.MRP)
            .HasPrecision(18, 2);

        builder.Property(product => product.DefaultSellingPrice)
            .HasPrecision(18, 2);

        builder.Property(product => product.DefaultPurchasePrice)
            .HasPrecision(18, 2);
    }
}
