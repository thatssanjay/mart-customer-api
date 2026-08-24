using Mart.Customer.Application.Inventory.Dtos;

namespace Mart.Customer.Application.Abstractions.Data;

public interface IProductRepository
{
    Task<IReadOnlyList<ProductListItemDto>> SearchActiveAsync(
        string? search,
        int maximumResults,
        CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<StockProductListItemDto> Products, int TotalCount)> SearchStockProductsAsync(
        long franchiseId,
        long martStoreId,
        string? search,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<StockProductDetailDto?> GetStockProductAsync(
        long productId,
        long franchiseId,
        long martStoreId,
        CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<LowStockProductDto> Products, int TotalCount)> GetLowStockProductsAsync(
        long franchiseId,
        long martStoreId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);
}
