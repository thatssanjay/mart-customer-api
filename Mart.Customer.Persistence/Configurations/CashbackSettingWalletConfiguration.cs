using Mart.Customer.Domain.Cashback;
using Mart.Customer.Domain.Wallets;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mart.Customer.Persistence.Configurations;

public sealed class CashbackSettingWalletConfiguration : IEntityTypeConfiguration<CashbackSettingWallet>
{
    public void Configure(EntityTypeBuilder<CashbackSettingWallet> builder)
    {
        builder.ToTable("CashbackSettingWallet", "mart");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.WalletTypeId).HasColumnName("WalletTypeID");
        builder.Property(x => x.PointPercentage).HasPrecision(18, 2);
        builder.Property(x => x.ConversionRate).HasPrecision(18, 2);
        builder.Property(x => x.StartDate).HasColumnType("datetime");
        builder.Property(x => x.EndDate).HasColumnType("datetime");
        builder.HasOne<CashbackConfiguration>().WithMany().HasForeignKey(x => x.CashbackSettingId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<WalletType>().WithMany().HasForeignKey(x => x.WalletTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.CashbackSettingId, x.WalletTypeId }).IsUnique()
            .HasDatabaseName("UX_CashbackSettingWallet_Setting_WalletType").HasFilter("[CashbackSettingId] IS NOT NULL");
    }
}
