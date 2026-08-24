namespace Mart.Customer.Api.Contracts.Inventory;

public sealed record StockInRequest
{
    public long ProductId { get; init; }

    public decimal Quantity { get; init; }

    public string MovementType { get; init; } = string.Empty;

    public string? ReferenceType { get; init; }

    public long? ReferenceId { get; init; }

    public string? BatchNumber { get; init; }

    public DateTime? ManufacturingDate { get; init; }

    public DateTime? ExpiryDate { get; init; }

    public decimal? PurchasePrice { get; init; }

    public decimal? SellingPrice { get; init; }

    public decimal? Mrp { get; init; }

    public string? Remarks { get; init; }

    public string? AdjustmentReason { get; init; }
}
