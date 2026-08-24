namespace Mart.Customer.Api.Contracts.Subscriptions;

public sealed class CreateCustomerSubscriptionRequest
{
    public long CustomerId { get; init; }
    public int SubscriptionPlanId { get; init; }
    public decimal? WalletCreditAmount { get; init; }
    public long? PointReferenceId { get; init; }
    public long? PaymentTransactionId { get; init; }
}
