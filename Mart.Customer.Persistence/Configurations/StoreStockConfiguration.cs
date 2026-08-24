using Mart.Customer.Domain.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mart.Customer.Persistence.Configurations;

public sealed class StoreStockConfiguration : IEntityTypeConfiguration<StoreStock>
{
    public void Configure(EntityTypeBuilder<StoreStock> builder)
    {
        builder.ToTable("StoreStock", "inventory");

        builder.HasKey(stock => stock.StoreStockId);

        builder.Property(stock => stock.CurrentQuantity)
            .HasPrecision(18, 3);

        builder.Property(stock => stock.LastPurchasePrice)
            .HasPrecision(18, 2);

        builder.Property(stock => stock.SellingPrice)
            .HasPrecision(18, 2);

        builder.HasIndex(stock => new { stock.FranchiseId, stock.MartStoreId, stock.ProductId })
            .IsUnique();
    }
}
