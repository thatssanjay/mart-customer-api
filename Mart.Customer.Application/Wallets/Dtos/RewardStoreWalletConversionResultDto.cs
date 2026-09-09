namespace Mart.Customer.Application.Wallets.Dtos;

public sealed record RewardStoreWalletConversionResultDto(
    string RequestId,
    long ConversionReferenceId,
    long StoreId,
    string StoreName,
    decimal ConversionRate,
    decimal RewardPointsDeducted,
    decimal ConvertedAmount,
    long RewardWalletId,
    decimal RewardBalance,
    long StoreWalletId,
    decimal StoreWalletBalance,
    long RewardTransactionId,
    string RewardTransactionNumber,
    long StoreWalletTransactionId,
    string StoreWalletTransactionNumber,
    DateTime ProcessedAt,
    bool IsIdempotentReplay);
