namespace Mart.Customer.Application.Inventory.Dtos;

public sealed record LowStockProductDto(
    long ProductId,
    string ProductCode,
    string ProductName,
    string? Barcode,
    decimal CurrentQuantity,
    long MinimumQuantity);
