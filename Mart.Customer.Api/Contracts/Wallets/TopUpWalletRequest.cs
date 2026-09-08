namespace Mart.Customer.Api.Contracts.Wallets;

public sealed class TopUpWalletRequest
{
    public string WalletCode { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public string PaymentMode { get; init; } = string.Empty;
    public string? CardLast4 { get; init; }
    public string? ReferenceNumber { get; init; }
    public string PaymentReference { get; init; } = string.Empty;
}
