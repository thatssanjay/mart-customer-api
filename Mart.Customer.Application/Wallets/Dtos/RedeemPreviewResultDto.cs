namespace Mart.Customer.Application.Wallets.Dtos;

public sealed record RedeemPreviewResultDto(
    long CustomerWalletId,
    long CustomerId,
    int WalletTypeId,
    decimal RequestedAmount,
    decimal BalanceBefore,
    decimal BalanceAfter,
    IReadOnlyList<RedeemPreviewBucketDto> BucketAllocations);
