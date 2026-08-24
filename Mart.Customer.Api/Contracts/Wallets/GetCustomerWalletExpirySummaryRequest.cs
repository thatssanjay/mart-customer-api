namespace Mart.Customer.Api.Contracts.Wallets;

public sealed class GetCustomerWalletExpirySummaryRequest
{
    public long CustomerId { get; init; }

    public int WalletTypeId { get; init; }
}
