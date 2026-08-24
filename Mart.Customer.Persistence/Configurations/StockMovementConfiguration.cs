using Mart.Customer.Domain.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mart.Customer.Persistence.Configurations;

public sealed class StockMovementConfiguration : IEntityTypeConfiguration<StockMovement>
{
    public void Configure(EntityTypeBuilder<StockMovement> builder)
    {
        builder.ToTable("StockMovement", "inventory");

        builder.HasKey(movement => movement.StockMovementId);

        builder.Property(movement => movement.MovementType)
            .HasMaxLength(50)
            .IsRequired();
        builder.Property(movement => movement.ReferenceType).HasMaxLength(50);
        builder.Property(movement => movement.Quantity).HasPrecision(18, 3);
        builder.Property(movement => movement.PreviousQuantity).HasPrecision(18, 3);
        builder.Property(movement => movement.NewQuantity).HasPrecision(18, 3);
        builder.Property(movement => movement.Remarks).HasMaxLength(500);
    }
}
