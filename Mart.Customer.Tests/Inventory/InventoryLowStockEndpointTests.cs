using System.Net.Http.Json;
using System.Reflection;
using Mart.Customer.Application.Customers.Dtos;
using Mart.Customer.Application.Inventory.Dtos;
using Mart.Customer.Domain.Inventory;

namespace Mart.Customer.Tests.Inventory;

public sealed class InventoryLowStockEndpointTests
{
    [Fact]
    public async Task GetLowStock_ReturnsProductsAtOrBelowMinimumWithPaging()
    {
        await using var factory = new InventoryStockApiFactory();
        await factory.SeedAsync(
            [
                CreateProduct(1, "P-1", "Alpha"),
                CreateProduct(2, "P-2", "Beta"),
                CreateProduct(3, "P-3", "Gamma")
            ],
            [
                CreateStoreStock(1, 7, 11, 1, 2m),
                CreateStoreStock(2, 7, 11, 2, 5m),
                CreateStoreStock(3, 7, 11, 3, 1m)
            ]);
        using var client = factory.CreateClient();

        var result = await client.GetFromJsonAsync<PagedResultDto<LowStockProductDto>>(
            "/api/v1/inventory/stock/low-stock?pageNumber=2&pageSize=2");

        Assert.NotNull(result);
        Assert.Equal(3, result.TotalCount);
        Assert.Equal(2, result.TotalPages);
        Assert.Equal(2, result.PageNumber);
        Assert.Equal(2, result.PageSize);
        var product = Assert.Single(result.Items);
        Assert.Equal(3, product.ProductId);
        Assert.Equal(1m, product.CurrentQuantity);
        Assert.Equal(5m, product.MinimumQuantity);
    }

    [Fact]
    public async Task GetLowStock_ExcludesNormalStock()
    {
        await using var factory = new InventoryStockApiFactory();
        await factory.SeedAsync(
            [CreateProduct(1, "P-1", "Normal")],
            [CreateStoreStock(1, 7, 11, 1, 6m)]);
        using var client = factory.CreateClient();

        var result = await client.GetFromJsonAsync<PagedResultDto<LowStockProductDto>>(
            "/api/v1/inventory/stock/low-stock");

        Assert.NotNull(result);
        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
    }

    [Fact]
    public async Task GetLowStock_ExcludesInactiveProducts()
    {
        await using var factory = new InventoryStockApiFactory();
        await factory.SeedAsync(
            [CreateProduct(1, "P-1", "Inactive", isActive: false)],
            [CreateStoreStock(1, 7, 11, 1, 1m)]);
        using var client = factory.CreateClient();

        var result = await client.GetFromJsonAsync<PagedResultDto<LowStockProductDto>>(
            "/api/v1/inventory/stock/low-stock");

        Assert.NotNull(result);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task GetLowStock_DoesNotReturnQuantitiesFromOtherStoresOrFranchises()
    {
        await using var factory = new InventoryStockApiFactory();
        await factory.SeedAsync(
            [
                CreateProduct(1, "P-1", "Authorized"),
                CreateProduct(2, "P-2", "Other store"),
                CreateProduct(3, "P-3", "Other franchise")
            ],
            [
                CreateStoreStock(1, 7, 11, 1, 1m),
                CreateStoreStock(2, 7, 12, 2, 1m),
                CreateStoreStock(3, 8, 11, 3, 1m)
            ]);
        using var client = factory.CreateClient();

        var result = await client.GetFromJsonAsync<PagedResultDto<LowStockProductDto>>(
            "/api/v1/inventory/stock/low-stock");

        Assert.NotNull(result);
        Assert.Equal(3, result.TotalCount);
        Assert.Equal(1m, Assert.Single(result.Items, product => product.ProductId == 1).CurrentQuantity);
        Assert.Equal(0m, Assert.Single(result.Items, product => product.ProductId == 2).CurrentQuantity);
        Assert.Equal(0m, Assert.Single(result.Items, product => product.ProductId == 3).CurrentQuantity);
    }

    private static Product CreateProduct(
        long id,
        string code,
        string name,
        bool isActive = true)
    {
        var product = (Product)Activator.CreateInstance(typeof(Product), nonPublic: true)!;
        SetProperty(product, nameof(Product.ProductId), id);
        SetProperty(product, nameof(Product.ProductCode), code);
        SetProperty(product, nameof(Product.ProductName), name);
        SetProperty(product, nameof(Product.Barcode), $"BAR-{id}");
        SetProperty(product, nameof(Product.MinimumQuantity), 5L);
        SetProperty(product, nameof(Product.MaximumQuantity), 20L);
        SetProperty(product, nameof(Product.IsActive), isActive);
        SetProperty(product, nameof(Product.IsStockManaged), true);
        return product;
    }

    private static StoreStock CreateStoreStock(
        long id,
        long franchiseId,
        long storeId,
        long productId,
        decimal currentQuantity)
    {
        var stock = (StoreStock)Activator.CreateInstance(typeof(StoreStock), nonPublic: true)!;
        SetProperty(stock, nameof(StoreStock.StoreStockId), id);
        SetProperty(stock, nameof(StoreStock.FranchiseId), franchiseId);
        SetProperty(stock, nameof(StoreStock.MartStoreId), storeId);
        SetProperty(stock, nameof(StoreStock.ProductId), productId);
        SetProperty(stock, nameof(StoreStock.CurrentQuantity), currentQuantity);
        return stock;
    }

    private static void SetProperty<TTarget, TValue>(
        TTarget target,
        string propertyName,
        TValue value) =>
        typeof(TTarget)
            .GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public)!
            .SetValue(target, value);
}
