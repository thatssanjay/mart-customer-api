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

        builder.Property(product => product.Barcode)
            .HasMaxLength(100);

        builder.Property(product => product.HSNCode)
            .HasMaxLength(50);

        builder.Property(product => product.ProductType)
            .HasMaxLength(50);

        builder.Property(product => product.Description)
            .HasMaxLength(500);

        builder.Property(product => product.GSTPercent)
            .HasPrecision(5, 2);

        builder.Property(product => product.MRP)
            .HasPrecision(18, 2);

        builder.Property(product => product.DefaultSellingPrice)
            .HasPrecision(18, 2);

        builder.Property(product => product.DefaultPurchasePrice)
            .HasPrecision(18, 2);

        builder.Property(product => product.MinimumQuantity)
            .IsRequired();

        builder.Property(product => product.MaximumQuantity)
            .IsRequired();
    }
}
