using Mart.Customer.Application.Inventory.Dtos;

namespace Mart.Customer.Application.Abstractions.Data;

public interface IProductRepository
{
    Task<IReadOnlyList<ProductListItemDto>> SearchActiveAsync(
        string? search,
        int maximumResults,
        CancellationToken cancellationToken = default);
}
