namespace Mart.Customer.Api.Contracts.Inventory;

public sealed record StockOutRequest
{
    public long ProductId { get; init; }

    public decimal Quantity { get; init; }

    public string MovementType { get; init; } = string.Empty;

    public string? BatchNumber { get; init; }

    public string Reason { get; init; } = string.Empty;

    public string? Remarks { get; init; }
}
