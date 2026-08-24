namespace Mart.Customer.Application.Wallets.Dtos;

public sealed record CreditWalletResultDto(
    long WalletTransactionId,
    string TransactionNumber,
    long CustomerWalletId,
    long CustomerId,
    int WalletTypeId,
    decimal Amount,
    decimal BalanceBefore,
    decimal BalanceAfter,
    decimal TotalCredit,
    string? ReferenceType,
    long? ReferenceId,
    DateTime? ExpiryDate,
    DateTime TransactionDate);
