using Mart.Customer.Domain.Promotions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mart.Customer.Persistence.Configurations;

public sealed class StorePromotionConfiguration : IEntityTypeConfiguration<StorePromotion>
{
    public void Configure(EntityTypeBuilder<StorePromotion> builder)
    {
        builder.ToTable("StoreGameBonusConfig", "Wallet");
        builder.HasKey(promotion => promotion.Id);
        builder.Property(promotion => promotion.PromoCode).HasMaxLength(20).IsUnicode(false);
        builder.Property(promotion => promotion.BonusPoint).HasPrecision(18, 2);
        builder.Property(promotion => promotion.DiscountType).HasMaxLength(30).IsUnicode(false).IsRequired();
        builder.Property(promotion => promotion.BillPercentDiscount).HasPrecision(5, 2);
    }
}
