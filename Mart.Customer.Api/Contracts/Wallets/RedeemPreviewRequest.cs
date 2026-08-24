namespace Mart.Customer.Api.Contracts.Wallets;

public sealed class RedeemPreviewRequest
{
    public long CustomerId { get; init; }

    public int WalletTypeId { get; init; }

    public decimal Amount { get; init; }
}
