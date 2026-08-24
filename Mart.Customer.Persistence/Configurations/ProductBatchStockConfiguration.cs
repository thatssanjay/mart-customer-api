using Mart.Customer.Domain.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mart.Customer.Persistence.Configurations;

public sealed class ProductBatchStockConfiguration : IEntityTypeConfiguration<ProductBatchStock>
{
    public void Configure(EntityTypeBuilder<ProductBatchStock> builder)
    {
        builder.ToTable("ProductBatchStock", "inventory");

        builder.HasKey(stock => stock.ProductBatchStockId);

        builder.Property(stock => stock.BatchNumber)
            .HasMaxLength(100);

        builder.Property(stock => stock.ManufacturingDate)
            .HasColumnType("date");

        builder.Property(stock => stock.ExpiryDate)
            .HasColumnType("date");

        builder.Property(stock => stock.Quantity)
            .HasPrecision(18, 3);

        builder.Property(stock => stock.PurchasePrice)
            .HasPrecision(18, 2);

        builder.Property(stock => stock.SellingPrice)
            .HasPrecision(18, 2);

        builder.Property(stock => stock.MRP)
            .HasPrecision(18, 2);
    }
}
