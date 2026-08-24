namespace Mart.Customer.Api.Contracts.Wallets;

public sealed class GetCustomerWalletsRequest
{
    public bool IncludeInactive { get; init; }
}
