using Mart.Customer.Domain.Wallets;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mart.Customer.Persistence.Configurations;

public sealed class WalletTypeConfiguration : IEntityTypeConfiguration<WalletType>
{
    public void Configure(EntityTypeBuilder<WalletType> builder)
    {
        builder.ToTable("WalletType", "Wallet");

        builder.HasKey(walletType => walletType.Id);

        builder.Property(walletType => walletType.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(walletType => walletType.Code)
            .IsRequired()
            .HasMaxLength(50)
            .IsUnicode(false);

        builder.Property(walletType => walletType.Description)
            .HasMaxLength(500);

        builder.Property(walletType => walletType.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(walletType => walletType.CreatedDate)
            .IsRequired()
            .HasDefaultValueSql("sysutcdatetime()");
    }
}
