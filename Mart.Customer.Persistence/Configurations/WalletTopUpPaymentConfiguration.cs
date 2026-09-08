using Mart.Customer.Domain.Wallets;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mart.Customer.Persistence.Configurations;

public sealed class WalletTopUpPaymentConfiguration : IEntityTypeConfiguration<WalletTopUpPayment>
{
    public void Configure(EntityTypeBuilder<WalletTopUpPayment> builder)
    {
        builder.ToTable("WalletTopUpPayment", "Wallet");
        builder.HasKey(payment => payment.WalletTopUpPaymentId);
        builder.Property(payment => payment.WalletTopUpPaymentId).ValueGeneratedOnAdd();
        builder.Property(payment => payment.Amount).HasPrecision(18, 2);
        builder.Property(payment => payment.PreviousBalance).HasPrecision(18, 2);
        builder.Property(payment => payment.NewBalance).HasPrecision(18, 2);
        builder.Property(payment => payment.PaymentMode).HasMaxLength(20).IsUnicode(false).IsRequired();
        builder.Property(payment => payment.CardLast4).HasMaxLength(4).IsUnicode(false);
        builder.Property(payment => payment.ReferenceNumber).HasMaxLength(100).IsUnicode(false);
        builder.Property(payment => payment.PaymentReference).HasMaxLength(100).IsUnicode(false).IsRequired();
        builder.Property(payment => payment.Status).HasMaxLength(20).IsUnicode(false).IsRequired();
        builder.Property(payment => payment.WalletTransactionNumber).HasMaxLength(50).IsUnicode(false);
        builder.Property(payment => payment.CreatedOn).HasDefaultValueSql("sysutcdatetime()");
        builder.HasIndex(payment => payment.PaymentReference)
            .IsUnique().HasDatabaseName("UQ_WalletTopUpPayment_PaymentReference");
        builder.HasIndex(payment => new { payment.PaymentMode, payment.ReferenceNumber })
            .IsUnique().HasFilter("[ReferenceNumber] IS NOT NULL")
            .HasDatabaseName("UQ_WalletTopUpPayment_ExternalReference");
        builder.HasOne<CustomerWallet>().WithMany()
            .HasForeignKey(payment => payment.CustomerWalletId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<WalletTransaction>().WithOne()
            .HasForeignKey<WalletTopUpPayment>(payment => payment.WalletTransactionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
