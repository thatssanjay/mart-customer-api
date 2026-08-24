namespace Mart.Customer.Application.Subscriptions.Dtos;

public sealed record UpdatedSubscriptionPaymentDetailsDto(
    long Id,
    long CustomerId,
    bool? IsPointCreated,
    decimal? WalletCreditAmount,
    long? PointReferenceId,
    long? PaymentTransactionId);
