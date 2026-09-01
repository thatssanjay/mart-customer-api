namespace Mart.Customer.Api.Contracts.Payments;

public sealed class CreateWalletPaymentRequest
{
    public string? CartNumber { get; init; }
}
