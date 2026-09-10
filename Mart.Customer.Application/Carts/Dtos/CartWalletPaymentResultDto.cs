namespace Mart.Customer.Application.Carts.Dtos;

public sealed record CartWalletPaymentResultDto(
    long CustomerCartId,
    string CartNumber,
    string PaymentReference,
    string PaymentStatus,
    decimal PaidAmount,
    DateTime PaidOn,
    IReadOnlyList<CartWalletDeductionResultDto> Deductions);

public sealed record CartWalletDeductionResultDto(
    string WalletCode,
    long CustomerWalletId,
    long WalletTransactionId,
    string TransactionNumber,
    decimal Amount,
    decimal BalanceBefore,
    decimal BalanceAfter);
