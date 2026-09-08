namespace Mart.Customer.Application.Wallets.Dtos;

public sealed record WalletTopUpResultDto(
    long WalletTopUpPaymentId,
    long CustomerId,
    long CustomerWalletId,
    string WalletCode,
    decimal AddedAmount,
    decimal PreviousBalance,
    decimal NewBalance,
    string PaymentMode,
    string? CardLast4,
    string? ReferenceNumber,
    string PaymentReference,
    long WalletTransactionId,
    string WalletTransactionNumber,
    DateTime PaidOn);
