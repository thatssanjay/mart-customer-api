using Mart.Customer.Domain.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mart.Customer.Persistence.Configurations;

public sealed class StockAdjustmentConfiguration : IEntityTypeConfiguration<StockAdjustment>
{
    public void Configure(EntityTypeBuilder<StockAdjustment> builder)
    {
        builder.ToTable("StockAdjustment", "inventory");

        builder.HasKey(adjustment => adjustment.StockAdjustmentId);

        builder.Property(adjustment => adjustment.AdjustmentType)
            .HasMaxLength(50)
            .IsRequired();
        builder.Property(adjustment => adjustment.Quantity).HasPrecision(18, 3);
        builder.Property(adjustment => adjustment.Reason)
            .HasMaxLength(500)
            .IsRequired();
    }
}
