using Mart.Customer.Application.Inventory.Dtos;

namespace Mart.Customer.Application.Abstractions.Data;

public interface IStockMovementRepository
{
    Task<(IReadOnlyList<StockMovementDto> Movements, int TotalCount)> GetPagedAsync(
        long franchiseId,
        long martStoreId,
        long? productId,
        string? movementType,
        DateTime? fromDate,
        DateTime? toDate,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);
}
