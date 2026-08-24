using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using Mart.Customer.Api.Contracts.Inventory;
using Mart.Customer.Application.Inventory.Dtos;
using Mart.Customer.Domain.Inventory;
using Mart.Customer.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Mart.Customer.Tests.Inventory;

public sealed class InventoryStockOutEndpointTests
{
    [Fact]
    public async Task StockOut_InsufficientStoreStock_IsRejectedWithoutWrites()
    {
        await using var factory = new StockInApiFactory();
        await factory.SeedAsync(CreateProduct(), initialQuantity: 5m);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/inventory/stock/out",
            Request("DAMAGE", quantity: 6m));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertUnchangedAsync(factory, expectedStoreQuantity: 5m);
    }

    [Fact]
    public async Task StockOut_BatchWithInsufficientQuantity_IsRejectedWithoutWrites()
    {
        await using var factory = new StockInApiFactory();
        await factory.SeedAsync(
            CreateProduct(isBatchApplicable: true),
            initialQuantity: 10m,
            CreateBatch(storeId: 11, quantity: 2m));
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/inventory/stock/out",
            Request("DAMAGE", quantity: 3m, batchNumber: "BATCH-1"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await factory.AssertDatabaseAsync(dbContext =>
        {
            Assert.Equal(10m, Assert.Single(dbContext.StoreStocks).CurrentQuantity);
            Assert.Equal(2m, Assert.Single(dbContext.ProductBatchStocks).Quantity);
            Assert.Empty(dbContext.StockMovements);
            Assert.Empty(dbContext.StockAdjustments);
        });
    }

    [Fact]
    public async Task StockOut_BatchFromWrongStore_IsRejected()
    {
        await using var factory = new StockInApiFactory();
        await factory.SeedAsync(
            CreateProduct(isBatchApplicable: true),
            initialQuantity: 10m);
        await AddBatchAsync(factory, CreateBatch(storeId: 12, quantity: 5m));
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/inventory/stock/out",
            Request("DAMAGE", quantity: 2m, batchNumber: "BATCH-1"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await factory.AssertDatabaseAsync(dbContext =>
        {
            Assert.Equal(10m, Assert.Single(dbContext.StoreStocks).CurrentQuantity);
            Assert.Equal(12, Assert.Single(dbContext.ProductBatchStocks).MartStoreId);
            Assert.Empty(dbContext.StockMovements);
            Assert.Empty(dbContext.StockAdjustments);
        });
    }

    [Fact]
    public async Task StockOut_BatchManagedProductWithoutBatch_IsRejected()
    {
        await using var factory = new StockInApiFactory();
        await factory.SeedAsync(
            CreateProduct(isBatchApplicable: true),
            initialQuantity: 10m);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/inventory/stock/out",
            Request("DAMAGE", quantity: 2m));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertUnchangedAsync(factory, expectedStoreQuantity: 10m);
    }

    [Fact]
    public async Task StockOut_Damage_ReducesStoreAndCreatesAdjustmentAndMovement()
    {
        await using var factory = new StockInApiFactory();
        await factory.SeedAsync(CreateProduct(), initialQuantity: 10m);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/inventory/stock/out",
            Request("DAMAGE", quantity: 3m));

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<StockOutResultDto>();
        Assert.NotNull(result);
        Assert.Equal(10m, result.PreviousQuantity);
        Assert.Equal(7m, result.NewQuantity);
        await AssertSuccessfulRemovalAsync(factory, "DAMAGE", 3m, 10m, 7m);
    }

    [Fact]
    public async Task StockOut_Expired_ReducesStoreAndExpiredBatch()
    {
        await using var factory = new StockInApiFactory();
        await factory.SeedAsync(
            CreateProduct(isBatchApplicable: true, isExpiryApplicable: true),
            initialQuantity: 10m,
            CreateBatch(
                storeId: 11,
                quantity: 5m,
                expiryDate: DateTime.UtcNow.Date.AddDays(-1)));
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/inventory/stock/out",
            Request("EXPIRED", quantity: 2m, batchNumber: "BATCH-1"));

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<StockOutResultDto>();
        Assert.NotNull(result);
        Assert.Equal(10m, result.PreviousQuantity);
        Assert.Equal(8m, result.NewQuantity);
        Assert.Equal(5m, result.PreviousBatchQuantity);
        Assert.Equal(3m, result.NewBatchQuantity);
        await factory.AssertDatabaseAsync(dbContext =>
        {
            Assert.Equal(8m, Assert.Single(dbContext.StoreStocks).CurrentQuantity);
            Assert.Equal(3m, Assert.Single(dbContext.ProductBatchStocks).Quantity);
            Assert.Equal("EXPIRED", Assert.Single(dbContext.StockAdjustments).AdjustmentType);
            Assert.Equal("EXPIRED", Assert.Single(dbContext.StockMovements).MovementType);
        });
    }

    [Fact]
    public async Task StockOut_AdjustmentRemove_ReducesStockAndAuditsReason()
    {
        await using var factory = new StockInApiFactory();
        await factory.SeedAsync(CreateProduct(), initialQuantity: 9m);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/inventory/stock/out",
            Request("ADJUSTMENT_REMOVE", quantity: 4m));

        response.EnsureSuccessStatusCode();
        await AssertSuccessfulRemovalAsync(factory, "ADJUSTMENT_REMOVE", 4m, 9m, 5m);
    }

    [Fact]
    public async Task StockOut_Sale_IsRejectedWithoutWrites()
    {
        await using var factory = new StockInApiFactory();
        await factory.SeedAsync(CreateProduct(), initialQuantity: 10m);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/inventory/stock/out",
            Request("SALE", quantity: 2m));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertUnchangedAsync(factory, expectedStoreQuantity: 10m);
    }

    [Fact]
    public async Task StockOut_WhenSaveCompletionFails_RollsBackAllChanges()
    {
        await using var factory = new StockInApiFactory(new ThrowAfterStockInSaveInterceptor());
        await factory.SeedAsync(CreateProduct(), initialQuantity: 10m);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/inventory/stock/out",
            Request("DAMAGE", quantity: 2m));

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        await AssertUnchangedAsync(factory, expectedStoreQuantity: 10m);
    }

    [Fact]
    public async Task StockOut_ConcurrentRequests_CannotDriveStockNegative()
    {
        await using var factory = new StockInApiFactory();
        await factory.SeedAsync(CreateProduct(), initialQuantity: 5m);
        using var firstClient = factory.CreateClient();
        using var secondClient = factory.CreateClient();

        var responses = await Task.WhenAll(
            firstClient.PostAsJsonAsync(
                "/api/v1/inventory/stock/out",
                Request("DAMAGE", quantity: 3m)),
            secondClient.PostAsJsonAsync(
                "/api/v1/inventory/stock/out",
                Request("DAMAGE", quantity: 3m)));

        Assert.Single(responses, response => response.IsSuccessStatusCode);
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.BadRequest);
        await factory.AssertDatabaseAsync(dbContext =>
        {
            Assert.Equal(2m, Assert.Single(dbContext.StoreStocks).CurrentQuantity);
            Assert.Single(dbContext.StockMovements);
            Assert.Single(dbContext.StockAdjustments);
        });
    }

    private static StockOutRequest Request(
        string movementType,
        decimal quantity,
        string? batchNumber = null) => new()
    {
        ProductId = 1,
        Quantity = quantity,
        MovementType = movementType,
        BatchNumber = batchNumber,
        Reason = "Verified manual stock removal",
        Remarks = "Inventory review"
    };

    private static Product CreateProduct(
        bool isBatchApplicable = false,
        bool isExpiryApplicable = false)
    {
        var product = CreatePrivate<Product>();
        SetProperty(product, nameof(Product.ProductId), 1L);
        SetProperty(product, nameof(Product.ProductCode), "P-001");
        SetProperty(product, nameof(Product.ProductName), "Test product");
        SetProperty(product, nameof(Product.ProductType), "Grocery");
        SetProperty(product, nameof(Product.IsActive), true);
        SetProperty(product, nameof(Product.IsStockManaged), true);
        SetProperty(product, nameof(Product.IsBatchApplicable), isBatchApplicable);
        SetProperty(product, nameof(Product.IsExpiryApplicable), isExpiryApplicable);
        SetProperty(product, nameof(Product.DefaultPurchasePrice), 50m);
        SetProperty(product, nameof(Product.DefaultSellingPrice), 60m);
        SetProperty(product, nameof(Product.MRP), 65m);
        return product;
    }

    private static ProductBatchStock CreateBatch(
        long storeId,
        decimal quantity,
        DateTime? expiryDate = null)
    {
        var now = DateTime.UtcNow;
        var batch = CreatePrivate<ProductBatchStock>();
        SetProperty(batch, nameof(ProductBatchStock.FranchiseId), 7L);
        SetProperty(batch, nameof(ProductBatchStock.MartStoreId), storeId);
        SetProperty(batch, nameof(ProductBatchStock.ProductId), 1L);
        SetProperty(batch, nameof(ProductBatchStock.BatchNumber), "BATCH-1");
        SetProperty(batch, nameof(ProductBatchStock.ManufacturingDate), (DateTime?)now.Date.AddMonths(-6));
        SetProperty(batch, nameof(ProductBatchStock.ExpiryDate), expiryDate);
        SetProperty(batch, nameof(ProductBatchStock.Quantity), quantity);
        SetProperty(batch, nameof(ProductBatchStock.PurchasePrice), 50m);
        SetProperty(batch, nameof(ProductBatchStock.SellingPrice), 60m);
        SetProperty(batch, nameof(ProductBatchStock.MRP), 65m);
        SetProperty(batch, nameof(ProductBatchStock.IsActive), true);
        SetProperty(batch, nameof(ProductBatchStock.CreatedOn), now);
        return batch;
    }

    private static async Task AddBatchAsync(
        StockInApiFactory factory,
        ProductBatchStock batch)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await dbContext.ProductBatchStocks.AddAsync(batch);
        await dbContext.SaveChangesAsync();
    }

    private static Task AssertUnchangedAsync(
        StockInApiFactory factory,
        decimal expectedStoreQuantity) =>
        factory.AssertDatabaseAsync(dbContext =>
        {
            Assert.Equal(expectedStoreQuantity, Assert.Single(dbContext.StoreStocks).CurrentQuantity);
            Assert.Empty(dbContext.StockMovements);
            Assert.Empty(dbContext.StockAdjustments);
        });

    private static Task AssertSuccessfulRemovalAsync(
        StockInApiFactory factory,
        string movementType,
        decimal quantity,
        decimal previousQuantity,
        decimal newQuantity) =>
        factory.AssertDatabaseAsync(dbContext =>
        {
            Assert.Equal(newQuantity, Assert.Single(dbContext.StoreStocks).CurrentQuantity);
            var adjustment = Assert.Single(dbContext.StockAdjustments);
            Assert.Equal(movementType, adjustment.AdjustmentType);
            Assert.Equal(quantity, adjustment.Quantity);
            Assert.Equal("Verified manual stock removal", adjustment.Reason);
            var movement = Assert.Single(dbContext.StockMovements);
            Assert.Equal(movementType, movement.MovementType);
            Assert.Equal(quantity, movement.Quantity);
            Assert.Equal(previousQuantity, movement.PreviousQuantity);
            Assert.Equal(newQuantity, movement.NewQuantity);
            Assert.Equal(41, movement.CreatedBy);
        });

    private static T CreatePrivate<T>() where T : class =>
        (T)Activator.CreateInstance(typeof(T), nonPublic: true)!;

    private static void SetProperty<TTarget, TValue>(TTarget target, string name, TValue value) =>
        typeof(TTarget).GetProperty(name, BindingFlags.Instance | BindingFlags.Public)!.SetValue(target, value);
}
