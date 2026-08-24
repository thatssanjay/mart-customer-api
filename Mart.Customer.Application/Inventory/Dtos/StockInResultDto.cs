namespace Mart.Customer.Application.Inventory.Dtos;

public sealed record StockInResultDto(
    long StoreStockId,
    long? ProductBatchStockId,
    long StockMovementId,
    long? StockAdjustmentId,
    long ProductId,
    long FranchiseId,
    long MartStoreId,
    decimal Quantity,
    decimal PreviousQuantity,
    decimal NewQuantity,
    decimal? PreviousBatchQuantity,
    decimal? NewBatchQuantity,
    string MovementType,
    DateTime CreatedOn);
