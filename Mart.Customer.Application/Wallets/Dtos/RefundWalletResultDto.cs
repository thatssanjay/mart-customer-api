namespace Mart.Customer.Application.Wallets.Dtos;

public sealed record RefundWalletResultDto(
    long WalletTransactionId,
    string TransactionNumber,
    long OriginalWalletTransactionId,
    string OriginalTransactionNumber,
    long CustomerWalletId,
    long CustomerId,
    int WalletTypeId,
    decimal Amount,
    decimal BalanceBefore,
    decimal BalanceAfter,
    decimal RefundedAmount,
    decimal RemainingRefundableAmount,
    DateTime TransactionDate);
