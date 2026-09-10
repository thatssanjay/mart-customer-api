namespace Mart.Customer.Api.Contracts.Carts;

public sealed class PayCartWithWalletsRequest
{
    public string? CartNumber { get; init; }
    public string? PaymentToken { get; init; }
    public IReadOnlyList<CartWalletDeductionRequest> Deductions { get; init; } = [];
}

public sealed class CartWalletDeductionRequest
{
    public string? WalletCode { get; init; }
    public decimal Amount { get; init; }
}
