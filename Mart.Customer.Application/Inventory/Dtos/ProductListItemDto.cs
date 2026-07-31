namespace Mart.Customer.Application.Inventory.Dtos;

public sealed record ProductListItemDto(
    long ProductId,
    string ProductCode,
    string ProductName,
    decimal GSTPercent,
    decimal MRP,
    decimal DefaultSellingPrice,
    decimal DefaultPurchasePrice);
