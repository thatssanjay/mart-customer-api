namespace Mart.Customer.Application.Wallets.Dtos;

public sealed record WalletBalanceBucketDto(
    long WalletBalanceBucketId,
    long CustomerWalletId,
    long SourceTransactionId,
    decimal OriginalAmount,
    decimal AvailableAmount,
    DateTime? ExpiryDate,
    DateTime CreatedOn,
    WalletTransactionDto? SourceTransaction);
