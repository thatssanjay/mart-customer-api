using Mart.Customer.Domain.Wallets;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mart.Customer.Persistence.Configurations;

public sealed class WalletOperationComponentConfiguration : IEntityTypeConfiguration<WalletOperationComponent>
{
    public void Configure(EntityTypeBuilder<WalletOperationComponent> builder)
    {
        builder.ToTable("WalletOperationComponent", "Wallet");
        builder.HasKey(x => x.WalletOperationComponentId);
        builder.Property(x => x.ComponentCode).HasMaxLength(30).IsUnicode(false).IsRequired();
        builder.Property(x => x.AllocationKey).HasMaxLength(100).IsUnicode(false).IsRequired();
        builder.Property(x => x.CalculationSnapshotJson).HasMaxLength(16000).IsRequired();
        builder.Property(x => x.ExpiryDate).HasConversion(x => x,
            x => x.HasValue ? DateTime.SpecifyKind(x.Value, DateTimeKind.Utc) : (DateTime?)null);
        builder.HasOne<WalletOperation>().WithMany().HasForeignKey(x => x.WalletOperationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CustomerWallet>().WithMany().HasForeignKey(x => x.CustomerWalletId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Transaction).WithOne().HasForeignKey<WalletOperationComponent>(x => x.WalletTransactionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.WalletOperationId, x.CustomerWalletId, x.ComponentCode, x.AllocationKey })
            .IsUnique().HasDatabaseName("UQ_WalletOperationComponent_Identity");
    }
}
