namespace Mart.Customer.Api.Contracts.Wallets;

public sealed class GetCustomerWalletDetailRequest
{
    public long CustomerId { get; init; }

    public int WalletTypeId { get; init; }
}
