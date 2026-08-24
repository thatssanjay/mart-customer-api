namespace Mart.Customer.Application.Wallets.Dtos;

public sealed record WalletExpirySummaryDto(
    long CustomerWalletId,
    long CustomerId,
    int WalletTypeId,
    decimal TotalExpiringAmount,
    DateTime? NextExpiryDate,
    int ExpiringBucketCount);
