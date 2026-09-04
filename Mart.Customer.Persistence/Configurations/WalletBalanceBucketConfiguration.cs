using Mart.Customer.Domain.Wallets;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mart.Customer.Persistence.Configurations;

public sealed class WalletBalanceBucketConfiguration : IEntityTypeConfiguration<WalletBalanceBucket>
{
    public void Configure(EntityTypeBuilder<WalletBalanceBucket> builder)
    {
        builder.ToTable("WalletBalanceBucket", "Wallet", table =>
        {
            table.HasCheckConstraint("CK_WalletBalanceBucket_OriginalAmount", "CAST([OriginalAmount] AS decimal(18,2)) > 0");
            table.HasCheckConstraint("CK_WalletBalanceBucket_AvailableAmount", "CAST([AvailableAmount] AS decimal(18,2)) >= 0 AND CAST([AvailableAmount] AS decimal(18,2)) <= CAST([OriginalAmount] AS decimal(18,2))");
        });

        builder.HasKey(bucket => bucket.WalletBalanceBucketId);

        builder.Property(bucket => bucket.WalletBalanceBucketId)
            .ValueGeneratedOnAdd();

        builder.Property(bucket => bucket.OriginalAmount)
            .HasPrecision(18, 2);

        builder.Property(bucket => bucket.AvailableAmount)
            .HasPrecision(18, 2);

        builder.Property(bucket => bucket.CreatedOn)
            .HasDefaultValueSql("sysutcdatetime()");

        builder.HasOne(bucket => bucket.SourceTransaction)
            .WithMany()
            .HasForeignKey(bucket => bucket.SourceTransactionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(bucket => bucket.SourceTransactionId).IsUnique();
        builder.HasIndex(bucket => new { bucket.CustomerWalletId, bucket.ExpiryDate });
    }
}
