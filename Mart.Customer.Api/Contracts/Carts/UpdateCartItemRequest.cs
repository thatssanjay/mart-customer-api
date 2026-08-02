namespace Mart.Customer.Api.Contracts.Carts;

public sealed class UpdateCartItemRequest
{
    public string? ProductNameSnapshot { get; init; }
    public decimal Quantity { get; init; }
    public decimal UnitPrice { get; init; }
    public decimal MRP { get; init; }
    public decimal DiscountAmount { get; init; }
    public decimal GSTPercent { get; init; }
}
