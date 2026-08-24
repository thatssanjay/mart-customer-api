using Mart.Customer.Domain.Wallets;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mart.Customer.Persistence.Configurations;

public sealed class WalletBalanceBucketConfiguration : IEntityTypeConfiguration<WalletBalanceBucket>
{
    public void Configure(EntityTypeBuilder<WalletBalanceBucket> builder)
    {
        builder.ToTable("WalletBalanceBucket", "Wallet");

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
    }
}
