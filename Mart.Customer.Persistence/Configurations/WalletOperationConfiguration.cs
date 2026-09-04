using Mart.Customer.Domain.Wallets;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mart.Customer.Persistence.Configurations;

public sealed class WalletOperationConfiguration : IEntityTypeConfiguration<WalletOperation>
{
    public void Configure(EntityTypeBuilder<WalletOperation> builder)
    {
        builder.ToTable("WalletOperation", "Wallet", table =>
            table.HasCheckConstraint("CK_WalletOperation_Status", "([Status] = 'PROCESSING' AND [Outcome] IS NULL AND [CompletedOn] IS NULL) OR ([Status] = 'COMPLETED' AND [Outcome] IN ('CREDITED', 'NO_REWARD') AND [CompletedOn] IS NOT NULL)"));
        builder.HasKey(x => x.WalletOperationId);
        builder.Property(x => x.OperationNumber).HasMaxLength(50).IsUnicode(false).IsRequired();
        builder.Property(x => x.OperationKind).HasMaxLength(30).IsUnicode(false).IsRequired();
        builder.Property(x => x.BusinessKey).HasMaxLength(150).IsUnicode(false).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(20).IsUnicode(false).IsRequired();
        builder.Property(x => x.Outcome).HasMaxLength(20).IsUnicode(false);
        builder.Property(x => x.CalculationVersion).HasMaxLength(100).IsRequired();
        builder.Property(x => x.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(x => x.EffectiveAt).HasConversion(x => x, x => DateTime.SpecifyKind(x, DateTimeKind.Utc));
        builder.HasIndex(x => x.OperationNumber).IsUnique().HasDatabaseName("UQ_WalletOperation_Number");
        builder.HasIndex(x => new { x.OperationKind, x.BusinessKey }).IsUnique().HasDatabaseName("UQ_WalletOperation_Identity");
        builder.HasIndex(x => x.CustomerOrderId);
        builder.HasIndex(x => new { x.OperationKind, x.CustomerOrderId }).IsUnique()
            .HasDatabaseName("UQ_WalletOperation_Order");
        builder.HasIndex(x => x.CustomerOrderPaymentId);
    }
}
