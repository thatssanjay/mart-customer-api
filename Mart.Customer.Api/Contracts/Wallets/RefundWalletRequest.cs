namespace Mart.Customer.Api.Contracts.Wallets;

public sealed class RefundWalletRequest
{
    public long CustomerId { get; init; }

    public int WalletTypeId { get; init; }

    public string OriginalTransactionNumber { get; init; } = string.Empty;

    public decimal Amount { get; init; }

    public string? Remarks { get; init; }
}
