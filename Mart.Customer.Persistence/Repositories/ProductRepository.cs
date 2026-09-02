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
            var normalizedSearch = search.Trim();
            var barcodeProduct = await query
                .Where(product => product.Barcode == normalizedSearch)
                .Select(product => new ProductListItemDto(
                    product.ProductId,
                    product.ProductCode,
                    product.ProductName,
                    product.GSTPercent,
                    product.MRP,
                    product.DefaultSellingPrice,
                    product.DefaultPurchasePrice))
                .SingleOrDefaultAsync(cancellationToken);

            if (barcodeProduct is not null)
            {
                return [barcodeProduct];
            }

            var searchPattern = $"%{normalizedSearch}%";
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

    public async Task<(IReadOnlyList<StockProductListItemDto> Products, int TotalCount)> SearchStockProductsAsync(
        long franchiseId,
        long martStoreId,
        string? search,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var eligibleProducts = _dbContext.Products
            .AsNoTracking()
            .Where(product => product.IsActive && product.IsStockManaged);
        var normalizedSearch = search?.Trim();

        if (!string.IsNullOrWhiteSpace(normalizedSearch))
        {
            var barcodeProduct = await CreateStockProductQuery(
                    eligibleProducts.Where(product => product.Barcode == normalizedSearch),
                    franchiseId,
                    martStoreId)
                .SingleOrDefaultAsync(cancellationToken);

            if (barcodeProduct is not null)
            {
                return (pageNumber == 1 ? [barcodeProduct] : [], 1);
            }

            var searchPattern = $"%{normalizedSearch}%";
            eligibleProducts = eligibleProducts.Where(product =>
                EF.Functions.Like(product.ProductCode, searchPattern) ||
                EF.Functions.Like(product.ProductName, searchPattern));
        }

        var totalCount = await eligibleProducts.CountAsync(cancellationToken);
        var productsPage = eligibleProducts
            .OrderBy(product => product.ProductName)
            .ThenBy(product => product.ProductId)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize);
        var products = await CreateStockProductQuery(productsPage, franchiseId, martStoreId)
            .ToListAsync(cancellationToken);

        return (products, totalCount);
    }

    public async Task<StockProductDetailDto?> GetStockProductAsync(
        long productId,
        long franchiseId,
        long martStoreId,
        CancellationToken cancellationToken = default)
    {
        var product = await _dbContext.Products
            .AsNoTracking()
            .Where(item =>
                item.ProductId == productId &&
                item.IsActive &&
                item.IsStockManaged)
            .Select(item => new
            {
                item.ProductId,
                item.ProductCode,
                item.ProductName,
                item.CategoryId,
                item.BrandId,
                item.UnitId,
                item.Barcode,
                item.HSNCode,
                item.ProductType,
                item.Description,
                item.IsExpiryApplicable,
                item.IsBatchApplicable,
                item.GSTPercent,
                item.MRP,
                item.DefaultSellingPrice,
                item.DefaultPurchasePrice,
                item.MinimumQuantity,
                item.MaximumQuantity,
                item.IsActive,
                item.IsStockManaged
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (product is null)
        {
            return null;
        }

        var storeStock = await _dbContext.StoreStocks
            .AsNoTracking()
            .Where(stock =>
                stock.ProductId == productId &&
                stock.FranchiseId == franchiseId &&
                stock.MartStoreId == martStoreId)
            .Select(stock => new StoreStockDto(
                stock.StoreStockId,
                stock.CurrentQuantity,
                stock.LastPurchasePrice,
                stock.SellingPrice,
                stock.LastStockUpdatedOn,
                stock.IsActive))
            .SingleOrDefaultAsync(cancellationToken);

        var batches = await _dbContext.ProductBatchStocks
            .AsNoTracking()
            .Where(batch =>
                batch.ProductId == productId &&
                batch.FranchiseId == franchiseId &&
                batch.MartStoreId == martStoreId &&
                batch.IsActive)
            .OrderBy(batch => batch.ExpiryDate == null)
            .ThenBy(batch => batch.ExpiryDate)
            .ThenBy(batch => batch.ProductBatchStockId)
            .Select(batch => new ProductBatchStockDto(
                batch.ProductBatchStockId,
                batch.BatchNumber,
                batch.ManufacturingDate,
                batch.ExpiryDate,
                batch.Quantity,
                batch.PurchasePrice,
                batch.SellingPrice,
                batch.MRP))
            .ToListAsync(cancellationToken);

        return new StockProductDetailDto(
            product.ProductId,
            product.ProductCode,
            product.ProductName,
            product.CategoryId,
            product.BrandId,
            product.UnitId,
            product.Barcode,
            product.HSNCode,
            product.ProductType,
            product.Description,
            product.IsExpiryApplicable,
            product.IsBatchApplicable,
            product.GSTPercent,
            product.MRP,
            product.DefaultSellingPrice,
            product.DefaultPurchasePrice,
            product.MinimumQuantity,
            product.MaximumQuantity,
            product.IsActive,
            product.IsStockManaged,
            storeStock?.CurrentQuantity ?? 0m,
            storeStock,
            batches);
    }

    public async Task<(IReadOnlyList<LowStockProductDto> Products, int TotalCount)> GetLowStockProductsAsync(
        long franchiseId,
        long martStoreId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var storeStocks = _dbContext.StoreStocks
            .AsNoTracking()
            .Where(stock =>
                stock.FranchiseId == franchiseId &&
                stock.MartStoreId == martStoreId);

        var query =
            from product in _dbContext.Products.AsNoTracking()
            join stock in storeStocks
                on product.ProductId equals stock.ProductId into productStocks
            from stock in productStocks.DefaultIfEmpty()
            let currentQuantity = stock == null ? 0m : stock.CurrentQuantity
            where product.IsActive &&
                  product.IsStockManaged &&
                  currentQuantity <= product.MinimumQuantity
            select new
            {
                product.ProductId,
                product.ProductCode,
                product.ProductName,
                product.Barcode,
                CurrentQuantity = currentQuantity,
                product.MinimumQuantity
            };

        var totalCount = await query.CountAsync(cancellationToken);
        var products = await query
            .OrderBy(product => product.ProductName)
            .ThenBy(product => product.ProductId)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(product => new LowStockProductDto(
                product.ProductId,
                product.ProductCode,
                product.ProductName,
                product.Barcode,
                product.CurrentQuantity,
                product.MinimumQuantity))
            .ToListAsync(cancellationToken);

        return (products, totalCount);
    }

    private IQueryable<StockProductListItemDto> CreateStockProductQuery(
        IQueryable<Mart.Customer.Domain.Inventory.Product> products,
        long franchiseId,
        long martStoreId)
    {
        var storeStocks = _dbContext.StoreStocks
            .AsNoTracking()
            .Where(stock =>
                stock.FranchiseId == franchiseId &&
                stock.MartStoreId == martStoreId);

        return
            from product in products
            join stock in storeStocks on product.ProductId equals stock.ProductId into productStocks
            from stock in productStocks.DefaultIfEmpty()
            select new StockProductListItemDto(
                product.ProductId,
                product.ProductCode,
                product.ProductName,
                product.Barcode,
                stock == null ? 0m : stock.CurrentQuantity,
                product.MinimumQuantity,
                product.MaximumQuantity,
                (stock == null ? 0m : stock.CurrentQuantity) <= product.MinimumQuantity
                    ? "Low Stock"
                    : stock != null && stock.CurrentQuantity >= product.MaximumQuantity
                        ? "Over Stock"
                        : "Available");
    }
}
