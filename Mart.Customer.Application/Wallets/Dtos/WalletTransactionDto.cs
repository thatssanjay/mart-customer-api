namespace Mart.Customer.Application.Wallets.Dtos;

public sealed record WalletTransactionDto(
    long WalletTransactionId,
    string TransactionNumber,
    string TransactionType,
    decimal Amount,
    decimal BalanceBefore,
    decimal BalanceAfter,
    string? ReferenceType,
    long? ReferenceId,
    string? Remarks,
    DateTime TransactionDate);
