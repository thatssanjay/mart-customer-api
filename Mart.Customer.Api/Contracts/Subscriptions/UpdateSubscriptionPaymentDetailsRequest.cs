namespace Mart.Customer.Api.Contracts.Subscriptions;

public sealed class UpdateSubscriptionPaymentDetailsRequest
{
    public bool? IsPointCreated { get; init; }
    public decimal? WalletCreditAmount { get; init; }
    public long? PointReferenceId { get; init; }
    public long? PaymentTransactionId { get; init; }
}
