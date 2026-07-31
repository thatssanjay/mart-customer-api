using Mart.Customer.Application.Abstractions.Data;
using Mart.Customer.Application.Inventory.Dtos;
using Microsoft.EntityFrameworkCore;

namespace Mart.Customer.Persistence.Repositories;

internal sealed class ProductRepository : IProductRepository
{
    private readonly ApplicationDbContext _dbContext;

    public ProductRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<ProductListItemDto>> SearchActiveAsync(
        string? search,
        int maximumResults,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Products
            .AsNoTracking()
            .Where(product => product.IsActive);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchPattern = $"%{search.Trim()}%";
            query = query.Where(product =>
                EF.Functions.Like(product.ProductCode, searchPattern) ||
                EF.Functions.Like(product.ProductName, searchPattern));
        }

        return await query
            .OrderBy(product => product.ProductName)
            .ThenBy(product => product.ProductId)
            .Take(maximumResults)
            .Select(product => new ProductListItemDto(
                product.ProductId,
                product.ProductCode,
                product.ProductName,
                product.GSTPercent,
                product.MRP,
                product.DefaultSellingPrice,
                product.DefaultPurchasePrice))
            .ToListAsync(cancellationToken);
    }
}
