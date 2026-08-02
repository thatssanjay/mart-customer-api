namespace Mart.Customer.Api.Contracts.Carts;

public sealed class CreateCartItemRequest
{
    public long ProductId { get; init; }
    public string? ProductNameSnapshot { get; init; }
    public decimal Quantity { get; init; }
    public decimal UnitPrice { get; init; }
    public decimal MRP { get; init; }
    public decimal DiscountAmount { get; init; }
    public decimal GSTPercent { get; init; }
}
