using Mart.Customer.Domain.Wallets;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mart.Customer.Persistence.Configurations;

public sealed class WalletTransactionConfiguration : IEntityTypeConfiguration<WalletTransaction>
{
    public void Configure(EntityTypeBuilder<WalletTransaction> builder)
    {
        builder.ToTable("WalletTransaction", "Wallet");

        builder.HasKey(transaction => transaction.WalletTransactionId);

        builder.Property(transaction => transaction.WalletTransactionId)
            .ValueGeneratedOnAdd();

        builder.Property(transaction => transaction.TransactionNumber)
            .IsRequired()
            .HasMaxLength(50)
            .IsUnicode(false);

        builder.Property(transaction => transaction.TransactionType)
            .IsRequired()
            .HasMaxLength(30)
            .IsUnicode(false);

        builder.Property(transaction => transaction.Amount)
            .HasPrecision(18, 2);

        builder.Property(transaction => transaction.BalanceBefore)
            .HasPrecision(18, 2);

        builder.Property(transaction => transaction.BalanceAfter)
            .HasPrecision(18, 2);

        builder.Property(transaction => transaction.ReferenceType)
            .HasMaxLength(30)
            .IsUnicode(false);

        builder.Property(transaction => transaction.Remarks)
            .HasMaxLength(500)
            .IsUnicode(false);

        builder.Property(transaction => transaction.TransactionDate)
            .HasDefaultValueSql("sysutcdatetime()");

        builder.Property(transaction => transaction.CreatedBy)
            .HasMaxLength(100)
            .IsUnicode(false);

        builder.HasIndex(transaction => transaction.TransactionNumber)
            .IsUnique()
            .HasDatabaseName("UQ_WalletTransaction_TransactionNumber");
    }
}
