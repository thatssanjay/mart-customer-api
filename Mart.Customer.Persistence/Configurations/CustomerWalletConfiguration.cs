using Mart.Customer.Domain.Wallets;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mart.Customer.Persistence.Configurations;

public sealed class CustomerWalletConfiguration : IEntityTypeConfiguration<CustomerWallet>
{
    public void Configure(EntityTypeBuilder<CustomerWallet> builder)
    {
        builder.ToTable("CustomerWallet", "Wallet");

        builder.HasKey(wallet => wallet.CustomerWalletId);

        builder.Property(wallet => wallet.CustomerWalletId)
            .ValueGeneratedOnAdd();

        builder.Property(wallet => wallet.CurrentBalance)
            .HasPrecision(18, 2)
            .HasDefaultValue(0);

        builder.Property(wallet => wallet.TotalCredit)
            .HasPrecision(18, 2)
            .HasDefaultValue(0);

        builder.Property(wallet => wallet.TotalDebit)
            .HasPrecision(18, 2)
            .HasDefaultValue(0);

        builder.Property(wallet => wallet.TotalExpired)
            .HasPrecision(18, 2)
            .HasDefaultValue(0);

        builder.Property(wallet => wallet.IsActive)
            .HasDefaultValue(true);

        builder.Property(wallet => wallet.CreatedOn)
            .HasDefaultValueSql("sysutcdatetime()");

        builder.HasIndex(wallet => new { wallet.CustomerId, wallet.WalletTypeId })
            .IsUnique()
            .HasDatabaseName("UQ_CustomerWallet");
    }
}
