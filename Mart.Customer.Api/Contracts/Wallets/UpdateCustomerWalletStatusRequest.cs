namespace Mart.Customer.Api.Contracts.Wallets;

public sealed class UpdateCustomerWalletStatusRequest
{
    public bool? IsActive { get; init; }
}
