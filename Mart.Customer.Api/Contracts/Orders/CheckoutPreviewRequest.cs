namespace Mart.Customer.Api.Contracts.Orders;

public sealed class CheckoutPreviewRequest
{
    public string? CartNumber { get; init; }

    public int? WalletTypeId { get; init; }

    public decimal? RedemptionAmount { get; init; }
}
