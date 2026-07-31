using Mart.Customer.Domain.Cashback;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mart.Customer.Persistence.Configurations;

public sealed class CashbackConfigurationConfiguration
    : IEntityTypeConfiguration<CashbackConfiguration>
{
    public void Configure(EntityTypeBuilder<CashbackConfiguration> builder)
    {
        builder.ToTable("cashbackconfiguration", "mart");

        builder.HasKey(setting => setting.CashbackSettingId);

        builder.Property(setting => setting.CashbackPercentage)
            .HasPrecision(18, 2);

        builder.Property(setting => setting.MinimumPurchaseAmount)
            .HasPrecision(18, 2);

        builder.Property(setting => setting.MaximumCashbackPerOrder)
            .HasPrecision(18, 2);
    }
}
