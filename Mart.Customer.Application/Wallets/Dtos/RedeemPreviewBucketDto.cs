namespace Mart.Customer.Application.Wallets.Dtos;

public sealed record RedeemPreviewBucketDto(
    long WalletBalanceBucketId,
    long SourceTransactionId,
    decimal AvailableAmountBefore,
    decimal RedeemAmount,
    decimal AvailableAmountAfter,
    DateTime? ExpiryDate);
