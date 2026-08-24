using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using Mart.Customer.Application.Inventory.Dtos;
using Mart.Customer.Domain.Inventory;

namespace Mart.Customer.Tests.Inventory;

public sealed class InventoryStockProductDetailEndpointTests
{
    [Fact]
    public async Task Get_NormalProduct_ReturnsProductAndAuthorizedStoreStock()
    {
        await using var factory = new InventoryStockApiFactory();
        await factory.SeedAsync(
            [CreateProduct(1, isActive: true, isBatchApplicable: false)],
            [CreateStoreStock(1, 7, 11, 1, 14m)],
            []);
        using var client = factory.CreateClient();

        var result = await client.GetFromJsonAsync<StockProductDetailDto>(
            "/api/v1/inventory/stock/products/1");

        Assert.NotNull(result);
        Assert.Equal(1, result.ProductId);
        Assert.Equal("MILK-001", result.ProductCode);
        Assert.Equal("Whole Milk", result.ProductName);
        Assert.Equal("8901001", result.Barcode);
        Assert.Equal("0401", result.HSNCode);
        Assert.Equal("Dairy", result.ProductType);
        Assert.Equal(14m, result.CurrentQuantity);
        Assert.NotNull(result.StoreStock);
        Assert.Equal(14m, result.StoreStock.CurrentQuantity);
        Assert.Empty(result.Batches);
    }

    [Fact]
    public async Task Get_BatchProduct_ReturnsOnlyActiveBatchesByEarliestExpiry()
    {
        var earlyExpiry = new DateTime(2026, 9, 1);
        var lateExpiry = new DateTime(2026, 10, 1);
        await using var factory = new InventoryStockApiFactory();
        await factory.SeedAsync(
            [CreateProduct(1, isActive: true, isBatchApplicable: true)],
            [CreateStoreStock(1, 7, 11, 1, 10m)],
            [
                CreateBatchStock(1, 7, 11, 1, "LATE", lateExpiry, true),
                CreateBatchStock(2, 7, 11, 1, "NO-EXPIRY", null, true),
                CreateBatchStock(3, 7, 11, 1, "EARLY", earlyExpiry, true),
                CreateBatchStock(4, 7, 11, 1, "INACTIVE", earlyExpiry.AddDays(-1), false)
            ]);
        using var client = factory.CreateClient();

        var result = await client.GetFromJsonAsync<StockProductDetailDto>(
            "/api/v1/inventory/stock/products/1");

        Assert.NotNull(result);
        Assert.True(result.IsBatchApplicable);
        Assert.Equal(["EARLY", "LATE", "NO-EXPIRY"], result.Batches.Select(batch => batch.BatchNumber));
    }

    [Fact]
    public async Task Get_ProductWithoutStoreStock_ReturnsZeroCurrentQuantity()
    {
        await using var factory = new InventoryStockApiFactory();
        await factory.SeedAsync(CreateProduct(1, isActive: true, isBatchApplicable: false));
        using var client = factory.CreateClient();

        var result = await client.GetFromJsonAsync<StockProductDetailDto>(
            "/api/v1/inventory/stock/products/1");

        Assert.NotNull(result);
        Assert.Equal(0m, result.CurrentQuantity);
        Assert.Null(result.StoreStock);
    }

    [Fact]
    public async Task Get_InactiveProduct_ReturnsNotFound()
    {
        await using var factory = new InventoryStockApiFactory();
        await factory.SeedAsync(CreateProduct(1, isActive: false, isBatchApplicable: false));
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/inventory/stock/products/1");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Get_UsesOnlyStockAndBatchesFromAuthorizedStoreAndFranchise()
    {
        await using var factory = new InventoryStockApiFactory();
        await factory.SeedAsync(
            [CreateProduct(1, isActive: true, isBatchApplicable: true)],
            [
                CreateStoreStock(1, 7, 11, 1, 6m),
                CreateStoreStock(2, 7, 12, 1, 90m),
                CreateStoreStock(3, 8, 11, 1, 80m)
            ],
            [
                CreateBatchStock(1, 7, 11, 1, "AUTHORIZED", new DateTime(2026, 9, 1), true),
                CreateBatchStock(2, 7, 12, 1, "OTHER-STORE", new DateTime(2026, 8, 1), true),
                CreateBatchStock(3, 8, 11, 1, "OTHER-FRANCHISE", new DateTime(2026, 7, 1), true)
            ]);
        using var client = factory.CreateClient();

        var result = await client.GetFromJsonAsync<StockProductDetailDto>(
            "/api/v1/inventory/stock/products/1");

        Assert.NotNull(result);
        Assert.Equal(6m, result.CurrentQuantity);
        Assert.Equal("AUTHORIZED", Assert.Single(result.Batches).BatchNumber);
    }

    private static Product CreateProduct(long id, bool isActive, bool isBatchApplicable)
    {
        var product = (Product)Activator.CreateInstance(typeof(Product), nonPublic: true)!;
        SetProperty(product, nameof(Product.ProductId), id);
        SetProperty(product, nameof(Product.ProductCode), "MILK-001");
        SetProperty(product, nameof(Product.ProductName), "Whole Milk");
        SetProperty(product, nameof(Product.CategoryId), 10L);
        SetProperty(product, nameof(Product.BrandId), (long?)20);
        SetProperty(product, nameof(Product.UnitId), 30L);
        SetProperty(product, nameof(Product.Barcode), "8901001");
        SetProperty(product, nameof(Product.HSNCode), "0401");
        SetProperty(product, nameof(Product.ProductType), "Dairy");
        SetProperty(product, nameof(Product.Description), "One litre whole milk");
        SetProperty(product, nameof(Product.IsExpiryApplicable), true);
        SetProperty(product, nameof(Product.IsBatchApplicable), isBatchApplicable);
        SetProperty(product, nameof(Product.GSTPercent), 5m);
        SetProperty(product, nameof(Product.MRP), 70m);
        SetProperty(product, nameof(Product.DefaultSellingPrice), 65m);
        SetProperty(product, nameof(Product.DefaultPurchasePrice), 55m);
        SetProperty(product, nameof(Product.MinimumQuantity), 5L);
        SetProperty(product, nameof(Product.MaximumQuantity), 50L);
        SetProperty(product, nameof(Product.IsActive), isActive);
        SetProperty(product, nameof(Product.IsStockManaged), true);
        return product;
    }

    private static StoreStock CreateStoreStock(
        long id,
        long franchiseId,
        long storeId,
        long productId,
        decimal quantity)
    {
        var stock = (StoreStock)Activator.CreateInstance(typeof(StoreStock), nonPublic: true)!;
        SetProperty(stock, nameof(StoreStock.StoreStockId), id);
        SetProperty(stock, nameof(StoreStock.FranchiseId), franchiseId);
        SetProperty(stock, nameof(StoreStock.MartStoreId), storeId);
        SetProperty(stock, nameof(StoreStock.ProductId), productId);
        SetProperty(stock, nameof(StoreStock.CurrentQuantity), quantity);
        SetProperty(stock, nameof(StoreStock.LastPurchasePrice), (decimal?)55m);
        SetProperty(stock, nameof(StoreStock.SellingPrice), (decimal?)65m);
        SetProperty(stock, nameof(StoreStock.IsActive), true);
        return stock;
    }

    private static ProductBatchStock CreateBatchStock(
        long id,
        long franchiseId,
        long storeId,
        long productId,
        string batchNumber,
        DateTime? expiryDate,
        bool isActive)
    {
        var stock = (ProductBatchStock)Activator.CreateInstance(
            typeof(ProductBatchStock),
            nonPublic: true)!;
        SetProperty(stock, nameof(ProductBatchStock.ProductBatchStockId), id);
        SetProperty(stock, nameof(ProductBatchStock.FranchiseId), franchiseId);
        SetProperty(stock, nameof(ProductBatchStock.MartStoreId), storeId);
        SetProperty(stock, nameof(ProductBatchStock.ProductId), productId);
        SetProperty(stock, nameof(ProductBatchStock.BatchNumber), batchNumber);
        SetProperty(stock, nameof(ProductBatchStock.ManufacturingDate), (DateTime?)new DateTime(2026, 1, 1));
        SetProperty(stock, nameof(ProductBatchStock.ExpiryDate), expiryDate);
        SetProperty(stock, nameof(ProductBatchStock.Quantity), 5m);
        SetProperty(stock, nameof(ProductBatchStock.PurchasePrice), 55m);
        SetProperty(stock, nameof(ProductBatchStock.SellingPrice), 65m);
        SetProperty(stock, nameof(ProductBatchStock.MRP), 70m);
        SetProperty(stock, nameof(ProductBatchStock.IsActive), isActive);
        return stock;
    }

    private static void SetProperty<TTarget, TValue>(
        TTarget target,
        string propertyName,
        TValue value)
    {
        typeof(TTarget)
            .GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public)!
            .SetValue(target, value);
    }
}
