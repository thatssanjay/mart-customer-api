namespace Mart.Customer.Application.Inventory.Dtos;

public sealed record StockProductDetailDto(
    long ProductId,
    string ProductCode,
    string ProductName,
    long CategoryId,
    long? BrandId,
    long UnitId,
    string? Barcode,
    string? HSNCode,
    string ProductType,
    string? Description,
    bool IsExpiryApplicable,
    bool IsBatchApplicable,
    decimal GSTPercent,
    decimal MRP,
    decimal DefaultSellingPrice,
    decimal DefaultPurchasePrice,
    long MinimumQuantity,
    long MaximumQuantity,
    bool IsActive,
    bool IsStockManaged,
    decimal CurrentQuantity,
    StoreStockDto? StoreStock,
    IReadOnlyList<ProductBatchStockDto> Batches);

public sealed record StoreStockDto(
    long StoreStockId,
    decimal CurrentQuantity,
    decimal? LastPurchasePrice,
    decimal? SellingPrice,
    DateTime? LastStockUpdatedOn,
    bool IsActive);

public sealed record ProductBatchStockDto(
    long ProductBatchStockId,
    string? BatchNumber,
    DateTime? ManufacturingDate,
    DateTime? ExpiryDate,
    decimal Quantity,
    decimal PurchasePrice,
    decimal SellingPrice,
    decimal MRP);
