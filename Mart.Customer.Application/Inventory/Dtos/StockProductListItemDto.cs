namespace Mart.Customer.Application.Inventory.Dtos;

public sealed record StockProductListItemDto(
    long ProductId,
    string ProductCode,
    string ProductName,
    string? Barcode,
    decimal CurrentQuantity,
    long MinimumQty,
    long MaximumQty,
    string StockStatus);
