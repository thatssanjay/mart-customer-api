namespace Mart.Customer.Api.Contracts.Payments;

public sealed class GetWalletPaymentRequest
{
    public string? CartNumber { get; init; }
    public long? CustomerId { get; init; }
}
