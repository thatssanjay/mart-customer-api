namespace Mart.Customer.Api.Contracts.Orders;

public sealed class CheckoutOrderRequest
{
    public string? CartNumber { get; init; }
    public int? WalletTypeId { get; init; }
    public decimal? RedemptionAmount { get; init; }
    public string? WalletPaymentToken { get; init; }
    public IReadOnlyList<CheckoutPaymentRequest> Payments { get; init; } = [];
}

public sealed class CheckoutPaymentRequest
{
    public string? PaymentMode { get; init; }
    public decimal Amount { get; init; }
    public string? TransactionReference { get; init; }
}
