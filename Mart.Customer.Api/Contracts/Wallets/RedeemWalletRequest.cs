namespace Mart.Customer.Api.Contracts.Wallets;

public sealed class RedeemWalletRequest
{
    public long CustomerId { get; init; }

    public int WalletTypeId { get; init; }

    public decimal Amount { get; init; }

    public string? ReferenceType { get; init; }

    public long? ReferenceId { get; init; }

    public string? Remarks { get; init; }
}
