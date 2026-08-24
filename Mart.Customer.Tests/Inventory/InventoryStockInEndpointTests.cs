using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using Mart.Customer.Api.Contracts.Inventory;
using Mart.Customer.Application.Inventory.Dtos;
using Mart.Customer.Domain.Inventory;
using Mart.Customer.Persistence;
using Mart.Customer.Persistence.Auth;
using Mart.Customer.Persistence.MasterData;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Mart.Customer.Tests.Inventory;

public sealed class InventoryStockInEndpointTests
{
    [Fact]
    public async Task StockIn_DerivesContextAndQuantitiesAndCreatesMovement()
    {
        await using var factory = new StockInApiFactory();
        await factory.SeedAsync(CreateProduct(1), initialQuantity: 7m);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/inventory/stock/in",
            Request(quantity: 3m));

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<StockInResultDto>();
        Assert.NotNull(result);
        Assert.Equal(7m, result.PreviousQuantity);
        Assert.Equal(10m, result.NewQuantity);
        Assert.True(result.StoreStockId > 0);
        Assert.True(result.StockMovementId > 0);
        Assert.Null(result.StockAdjustmentId);

        await factory.AssertDatabaseAsync(dbContext =>
        {
            var stock = Assert.Single(dbContext.StoreStocks);
            Assert.Equal(10m, stock.CurrentQuantity);
            Assert.Equal(41, stock.ModifiedBy);

            var movement = Assert.Single(dbContext.StockMovements);
            Assert.Equal(7m, movement.PreviousQuantity);
            Assert.Equal(10m, movement.NewQuantity);
            Assert.Equal(41, movement.CreatedBy);
            Assert.Equal(7, movement.FranchiseId);
            Assert.Equal(11, movement.MartStoreId);
            Assert.Empty(dbContext.StockAdjustments);
        });
    }

    [Fact]
    public async Task StockIn_AdjustmentAdd_CreatesAdjustmentInSameTransaction()
    {
        await using var factory = new StockInApiFactory();
        await factory.SeedAsync(CreateProduct(1), initialQuantity: 2m);
        using var client = factory.CreateClient();
        var request = Request(quantity: 4m) with
        {
            MovementType = "ADJUSTMENT_ADD",
            AdjustmentReason = "Physical count correction"
        };

        var response = await client.PostAsJsonAsync("/api/v1/inventory/stock/in", request);

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<StockInResultDto>();
        Assert.NotNull(result);
        Assert.True(result.StockAdjustmentId > 0);
        await factory.AssertDatabaseAsync(dbContext =>
        {
            var adjustment = Assert.Single(dbContext.StockAdjustments);
            Assert.Equal("ADJUSTMENT_ADD", adjustment.AdjustmentType);
            Assert.Equal("Physical count correction", adjustment.Reason);
            Assert.Single(dbContext.StockMovements);
            Assert.Equal(6m, Assert.Single(dbContext.StoreStocks).CurrentQuantity);
        });
    }

    [Fact]
    public async Task StockIn_BatchManagedProduct_UpdatesStoreAndBatchTogether()
    {
        var manufacturingDate = DateTime.UtcNow.Date.AddMonths(-1);
        var expiryDate = DateTime.UtcNow.Date.AddYears(1);
        await using var factory = new StockInApiFactory();
        await factory.SeedAsync(
            CreateProduct(1, isBatchApplicable: true, isExpiryApplicable: true),
            initialQuantity: 5m,
            CreateBatch(1, 5m, manufacturingDate, expiryDate));
        using var client = factory.CreateClient();
        var request = Request(quantity: 2m) with
        {
            BatchNumber = "BATCH-1",
            ManufacturingDate = manufacturingDate,
            ExpiryDate = expiryDate
        };

        var response = await client.PostAsJsonAsync("/api/v1/inventory/stock/in", request);

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<StockInResultDto>();
        Assert.NotNull(result);
        Assert.Equal(5m, result.PreviousBatchQuantity);
        Assert.Equal(7m, result.NewBatchQuantity);
        await factory.AssertDatabaseAsync(dbContext =>
        {
            Assert.Equal(7m, Assert.Single(dbContext.StoreStocks).CurrentQuantity);
            Assert.Equal(7m, Assert.Single(dbContext.ProductBatchStocks).Quantity);
            Assert.Single(dbContext.StockMovements);
        });
    }

    [Fact]
    public async Task StockIn_WhenSaveCompletionFails_RollsBackEveryInventoryWrite()
    {
        var interceptor = new ThrowAfterStockInSaveInterceptor();
        await using var factory = new StockInApiFactory(interceptor);
        await factory.SeedAsync(CreateProduct(1), initialQuantity: 8m);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/inventory/stock/in",
            Request(quantity: 2m));

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        await factory.AssertDatabaseAsync(dbContext =>
        {
            Assert.Equal(8m, Assert.Single(dbContext.StoreStocks).CurrentQuantity);
            Assert.Empty(dbContext.StockMovements);
            Assert.Empty(dbContext.StockAdjustments);
            Assert.Empty(dbContext.ProductBatchStocks);
        });
    }

    [Fact]
    public async Task StockIn_ConcurrentRequests_DoNotLoseQuantityOrAuditMovements()
    {
        await using var factory = new StockInApiFactory();
        await factory.SeedAsync(CreateProduct(1), initialQuantity: 10m);
        using var firstClient = factory.CreateClient();
        using var secondClient = factory.CreateClient();

        var responses = await Task.WhenAll(
            firstClient.PostAsJsonAsync("/api/v1/inventory/stock/in", Request(quantity: 2m)),
            secondClient.PostAsJsonAsync("/api/v1/inventory/stock/in", Request(quantity: 3m)));

        Assert.All(responses, response => response.EnsureSuccessStatusCode());
        await factory.AssertDatabaseAsync(dbContext =>
        {
            Assert.Equal(15m, Assert.Single(dbContext.StoreStocks).CurrentQuantity);
            var movements = dbContext.StockMovements.OrderBy(item => item.StockMovementId).ToArray();
            Assert.Equal(2, movements.Length);
            Assert.Equal(5m, movements.Sum(item => item.Quantity));
            Assert.Contains(movements, item => item.NewQuantity == 15m);
        });
    }

    [Fact]
    public async Task StockIn_ClientSuppliedPreviousAndNewQuantities_AreIgnored()
    {
        await using var factory = new StockInApiFactory();
        await factory.SeedAsync(CreateProduct(1), initialQuantity: 4m);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/inventory/stock/in",
            new
            {
                ProductId = 1,
                Quantity = 2m,
                MovementType = "PURCHASE",
                PreviousQuantity = 900m,
                NewQuantity = 999m
            });

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<StockInResultDto>();
        Assert.NotNull(result);
        Assert.Equal(4m, result.PreviousQuantity);
        Assert.Equal(6m, result.NewQuantity);
        await factory.AssertDatabaseAsync(dbContext =>
        {
            var movement = Assert.Single(dbContext.StockMovements);
            Assert.Equal(4m, movement.PreviousQuantity);
            Assert.Equal(6m, movement.NewQuantity);
        });
    }

    [Fact]
    public async Task StockIn_ZeroQuantity_IsRejectedWithoutWrites()
    {
        await using var factory = new StockInApiFactory();
        await factory.SeedAsync(CreateProduct(1), initialQuantity: 4m);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/inventory/stock/in",
            Request(quantity: 0m));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await factory.AssertDatabaseAsync(dbContext =>
        {
            Assert.Equal(4m, Assert.Single(dbContext.StoreStocks).CurrentQuantity);
            Assert.Empty(dbContext.StockMovements);
        });
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task StockIn_InactiveOrNonStockManagedProduct_IsRejected(
        bool isActive,
        bool isStockManaged)
    {
        await using var factory = new StockInApiFactory();
        await factory.SeedAsync(
            CreateProduct(1, isActive: isActive, isStockManaged: isStockManaged),
            initialQuantity: 4m);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/inventory/stock/in",
            Request(quantity: 1m));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await factory.AssertDatabaseAsync(dbContext =>
        {
            Assert.Equal(4m, Assert.Single(dbContext.StoreStocks).CurrentQuantity);
            Assert.Empty(dbContext.StockMovements);
        });
    }

    private static StockInRequest Request(decimal quantity) => new()
    {
        ProductId = 1,
        Quantity = quantity,
        MovementType = "PURCHASE",
        ReferenceType = "PURCHASE",
        ReferenceId = 7001,
        PurchasePrice = 50m,
        SellingPrice = 60m,
        Mrp = 65m,
        Remarks = "Goods received"
    };

    private static Product CreateProduct(
        long id,
        bool isBatchApplicable = false,
        bool isExpiryApplicable = false,
        bool isActive = true,
        bool isStockManaged = true)
    {
        var product = CreatePrivate<Product>();
        SetProperty(product, nameof(Product.ProductId), id);
        SetProperty(product, nameof(Product.ProductCode), "P-001");
        SetProperty(product, nameof(Product.ProductName), "Test product");
        SetProperty(product, nameof(Product.ProductType), "Grocery");
        SetProperty(product, nameof(Product.IsActive), isActive);
        SetProperty(product, nameof(Product.IsStockManaged), isStockManaged);
        SetProperty(product, nameof(Product.IsBatchApplicable), isBatchApplicable);
        SetProperty(product, nameof(Product.IsExpiryApplicable), isExpiryApplicable);
        SetProperty(product, nameof(Product.DefaultPurchasePrice), 50m);
        SetProperty(product, nameof(Product.DefaultSellingPrice), 60m);
        SetProperty(product, nameof(Product.MRP), 65m);
        return product;
    }

    private static ProductBatchStock CreateBatch(
        long id,
        decimal quantity,
        DateTime manufacturingDate,
        DateTime expiryDate)
    {
        var batch = CreatePrivate<ProductBatchStock>();
        SetProperty(batch, nameof(ProductBatchStock.ProductBatchStockId), id);
        SetProperty(batch, nameof(ProductBatchStock.FranchiseId), 7L);
        SetProperty(batch, nameof(ProductBatchStock.MartStoreId), 11L);
        SetProperty(batch, nameof(ProductBatchStock.ProductId), 1L);
        SetProperty(batch, nameof(ProductBatchStock.BatchNumber), "BATCH-1");
        SetProperty(batch, nameof(ProductBatchStock.ManufacturingDate), (DateTime?)manufacturingDate);
        SetProperty(batch, nameof(ProductBatchStock.ExpiryDate), (DateTime?)expiryDate);
        SetProperty(batch, nameof(ProductBatchStock.Quantity), quantity);
        SetProperty(batch, nameof(ProductBatchStock.PurchasePrice), 50m);
        SetProperty(batch, nameof(ProductBatchStock.SellingPrice), 60m);
        SetProperty(batch, nameof(ProductBatchStock.MRP), 65m);
        SetProperty(batch, nameof(ProductBatchStock.IsActive), true);
        SetProperty(batch, nameof(ProductBatchStock.CreatedOn), manufacturingDate);
        return batch;
    }

    private static T CreatePrivate<T>() where T : class =>
        (T)Activator.CreateInstance(typeof(T), nonPublic: true)!;

    private static void SetProperty<TTarget, TValue>(TTarget target, string name, TValue value) =>
        typeof(TTarget).GetProperty(name, BindingFlags.Instance | BindingFlags.Public)!.SetValue(target, value);
}

internal sealed class StockInApiFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString =
        $"Data Source=stock-in-{Guid.NewGuid():N};Mode=Memory;Cache=Shared;Default Timeout=30";
    private readonly SqliteConnection _anchorConnection;
    private readonly SaveChangesInterceptor? _interceptor;

    public StockInApiFactory(SaveChangesInterceptor? interceptor = null)
    {
        _interceptor = interceptor;
        _anchorConnection = new SqliteConnection(_connectionString);
        _anchorConnection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            var dbContextConfigurationDescriptors = services
                .Where(descriptor =>
                    descriptor.ServiceType.IsGenericType &&
                    descriptor.ServiceType.Name == "IDbContextOptionsConfiguration`1" &&
                    descriptor.ServiceType.GenericTypeArguments[0] == typeof(ApplicationDbContext))
                .ToList();
            foreach (var descriptor in dbContextConfigurationDescriptors)
            {
                services.Remove(descriptor);
            }

            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.AddDbContext<ApplicationDbContext>(options =>
            {
                options.UseSqlite(_connectionString);
                if (_interceptor is not null)
                {
                    options.AddInterceptors(_interceptor);
                }
            });

            services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = InventoryStockAuthenticationHandler.AuthenticationScheme;
                    options.DefaultChallengeScheme = InventoryStockAuthenticationHandler.AuthenticationScheme;
                })
                .AddScheme<AuthenticationSchemeOptions, InventoryStockAuthenticationHandler>(
                    InventoryStockAuthenticationHandler.AuthenticationScheme,
                    _ => { });
        });
    }

    public async Task SeedAsync(
        Product product,
        decimal initialQuantity,
        ProductBatchStock? batch = null)
    {
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await dbContext.Database.EnsureCreatedAsync();

        var user = new UserMasterEntity
        {
            UserId = 41,
            UserCode = "U-41",
            FirstName = "Stock",
            LastName = "User",
            DisplayName = "Stock User",
            Username = "stock-user",
            IsActive = true
        };
        var access = new UserMartAccessEntity
        {
            UserMartAccessId = 1,
            UserId = 41,
            FranchiseId = 7,
            MartStoreId = 11,
            CanAccess = true
        };
        var store = CreatePrivate<MartStoreEntity>();
        SetProperty(store, nameof(MartStoreEntity.StoreId), 11L);
        SetProperty(store, nameof(MartStoreEntity.FranchiseId), 7L);
        SetProperty(store, nameof(MartStoreEntity.StoreName), "Test store");
        SetProperty(store, nameof(MartStoreEntity.IsActive), true);

        var stock = StoreStock.Create(7, 11, product.ProductId, initialQuantity, 50m, 60m, 41, DateTime.UtcNow);
        await dbContext.Users.AddAsync(user);
        await dbContext.UserMartAccesses.AddAsync(access);
        await dbContext.MartStores.AddAsync(store);
        await dbContext.Products.AddAsync(product);
        await dbContext.StoreStocks.AddAsync(stock);
        if (batch is not null)
        {
            await dbContext.ProductBatchStocks.AddAsync(batch);
        }

        await dbContext.SaveChangesAsync();
    }

    public async Task AssertDatabaseAsync(Action<ApplicationDbContext> assertion)
    {
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        assertion(dbContext);
        await Task.CompletedTask;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _anchorConnection.Dispose();
        }
    }

    private static T CreatePrivate<T>() where T : class =>
        (T)Activator.CreateInstance(typeof(T), nonPublic: true)!;

    private static void SetProperty<TTarget, TValue>(TTarget target, string name, TValue value) =>
        typeof(TTarget).GetProperty(name, BindingFlags.Instance | BindingFlags.Public)!.SetValue(target, value);
}

internal sealed class ThrowAfterStockInSaveInterceptor : SaveChangesInterceptor
{
    private int _throwOnCompletion;

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context?.ChangeTracker.Entries<StockMovement>()
            .Any(entry => entry.State == EntityState.Added) == true)
        {
            Interlocked.Exchange(ref _throwOnCompletion, 1);
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref _throwOnCompletion, 0) == 1)
        {
            throw new InvalidOperationException("Injected failure after inventory SQL execution.");
        }

        return base.SavedChangesAsync(eventData, result, cancellationToken);
    }
}
