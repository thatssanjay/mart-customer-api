namespace Mart.Customer.Application.Wallets.Dtos;

public sealed record RedeemWalletResultDto(
    long WalletTransactionId,
    string TransactionNumber,
    long CustomerWalletId,
    long CustomerId,
    int WalletTypeId,
    decimal Amount,
    decimal BalanceBefore,
    decimal BalanceAfter,
    decimal TotalDebit,
    string? ReferenceType,
    long? ReferenceId,
    DateTime TransactionDate);
