using Mart.Customer.Application.Abstractions.Data;
using Mart.Customer.Application.Inventory.Dtos;
using Microsoft.EntityFrameworkCore;

namespace Mart.Customer.Persistence.Repositories;

internal sealed class StockMovementRepository : IStockMovementRepository
{
    private readonly ApplicationDbContext _dbContext;

    public StockMovementRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<(IReadOnlyList<StockMovementDto> Movements, int TotalCount)> GetPagedAsync(
        long franchiseId,
        long martStoreId,
        long? productId,
        string? movementType,
        DateTime? fromDate,
        DateTime? toDate,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var movements = _dbContext.StockMovements
            .AsNoTracking()
            .Where(movement =>
                movement.FranchiseId == franchiseId &&
                movement.MartStoreId == martStoreId);

        if (productId.HasValue)
        {
            movements = movements.Where(movement => movement.ProductId == productId.Value);
        }

        if (!string.IsNullOrWhiteSpace(movementType))
        {
            var normalizedMovementType = movementType.Trim().ToUpperInvariant();
            movements = movements.Where(movement => movement.MovementType == normalizedMovementType);
        }

        if (fromDate.HasValue)
        {
            movements = movements.Where(movement => movement.CreatedOn >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            movements = movements.Where(movement => movement.CreatedOn <= toDate.Value);
        }

        var totalCount = await movements.CountAsync(cancellationToken);
        var products = _dbContext.Products.AsNoTracking();
        var items = await (
                from movement in movements
                join product in products on movement.ProductId equals product.ProductId
                orderby movement.CreatedOn descending, movement.StockMovementId descending
                select new StockMovementDto(
                    movement.StockMovementId,
                    movement.ProductId,
                    product.ProductCode,
                    product.ProductName,
                    movement.MovementType,
                    movement.ReferenceType,
                    movement.ReferenceId,
                    movement.Quantity,
                    movement.PreviousQuantity,
                    movement.NewQuantity,
                    movement.Remarks,
                    movement.CreatedBy,
                    movement.CreatedOn))
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }
}
