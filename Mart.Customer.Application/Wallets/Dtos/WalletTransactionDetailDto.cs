namespace Mart.Customer.Application.Wallets.Dtos;

public sealed record WalletTransactionDetailDto(
    long WalletTransactionId,
    string TransactionNumber,
    long CustomerWalletId,
    long CustomerId,
    int WalletTypeId,
    string WalletTypeName,
    string WalletTypeCode,
    bool IsWalletActive,
    bool IsWalletTypeActive,
    string TransactionType,
    decimal Amount,
    decimal BalanceBefore,
    decimal BalanceAfter,
    string? ReferenceType,
    long? ReferenceId,
    string? Remarks,
    DateTime TransactionDate,
    string? CreatedBy);
