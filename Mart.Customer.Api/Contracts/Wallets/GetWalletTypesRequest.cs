namespace Mart.Customer.Api.Contracts.Wallets;

public sealed class GetWalletTypesRequest
{
    public bool ActiveOnly { get; init; } = true;
}
