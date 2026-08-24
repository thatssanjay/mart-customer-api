namespace Mart.Customer.Api.Contracts.Wallets;

public sealed class GetCustomerWalletBucketsRequest
{
    public bool AvailableOnly { get; init; }

    public bool IncludeSourceTransaction { get; init; }
}
