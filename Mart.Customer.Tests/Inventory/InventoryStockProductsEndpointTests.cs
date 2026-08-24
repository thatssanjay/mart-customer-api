using System.Net.Http.Json;
using System.Reflection;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Mart.Customer.Application.Abstractions.Data;
using Mart.Customer.Application.Auth.Dtos;
using Mart.Customer.Application.Customers.Dtos;
using Mart.Customer.Application.Inventory.Dtos;
using Mart.Customer.Domain.Inventory;
using Mart.Customer.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Mart.Customer.Tests.Inventory;

public sealed class InventoryStockProductsEndpointTests
{
    [Fact]
    public async Task Search_WhenBarcodeMatchesExactly_ReturnsOnlyBarcodeProduct()
    {
        await using var factory = new InventoryStockApiFactory();
        await factory.SeedAsync(
            CreateProduct(1, "CODE-1", "Exact barcode product", "890123", true, true),
            CreateProduct(2, "890123-CODE", "Code fallback product", "OTHER", true, true));
        using var client = factory.CreateClient();

        var result = await client.GetFromJsonAsync<PagedResultDto<StockProductListItemDto>>(
            "/api/v1/inventory/stock/products?search=890123");

        Assert.NotNull(result);
        var product = Assert.Single(result.Items);
        Assert.Equal(1, product.ProductId);
        Assert.Equal(1, result.TotalCount);
    }

    [Fact]
    public async Task Search_WhenNoBarcodeMatches_FindsProductCode()
    {
        await using var factory = new InventoryStockApiFactory();
        await factory.SeedAsync(
            CreateProduct(1, "MILK-001", "Whole Milk", "111", true, true),
            CreateProduct(2, "BREAD-001", "Bread", "222", true, true));
        using var client = factory.CreateClient();

        var result = await client.GetFromJsonAsync<PagedResultDto<StockProductListItemDto>>(
            "/api/v1/inventory/stock/products?search=MILK");

        Assert.NotNull(result);
        Assert.Equal("MILK-001", Assert.Single(result.Items).ProductCode);
    }

    [Fact]
    public async Task GetStockProducts_ReturnsProductMinimumAndMaximumQuantities()
    {
        await using var factory = new InventoryStockApiFactory();
        var seededProduct = CreateProduct(1, "RICE-001", "Basmati Rice", "111", true, true);
        SetProperty(seededProduct, nameof(Product.MinimumQuantity), 5L);
        SetProperty(seededProduct, nameof(Product.MaximumQuantity), 30L);
        await factory.SeedAsync(seededProduct);
        using var client = factory.CreateClient();

        var result = await client.GetFromJsonAsync<PagedResultDto<StockProductListItemDto>>(
            "/api/v1/inventory/stock/products");

        Assert.NotNull(result);
        var product = Assert.Single(result.Items);
        Assert.Equal(5L, product.MinimumQty);
        Assert.Equal(30L, product.MaximumQty);
    }

    [Fact]
    public async Task Search_WhenNoBarcodeMatches_FindsProductNameAndDefaultsMissingStockToZero()
    {
        await using var factory = new InventoryStockApiFactory();
        await factory.SeedAsync(CreateProduct(1, "RICE-001", "Basmati Rice", "111", true, true));
        using var client = factory.CreateClient();

        var result = await client.GetFromJsonAsync<PagedResultDto<StockProductListItemDto>>(
            "/api/v1/inventory/stock/products?search=Basmati");

        Assert.NotNull(result);
        var product = Assert.Single(result.Items);
        Assert.Equal(0m, product.CurrentQuantity);
        Assert.Equal("Low Stock", product.StockStatus);
    }

    [Fact]
    public async Task Search_WhenNothingMatches_ReturnsEmptyPage()
    {
        await using var factory = new InventoryStockApiFactory();
        await factory.SeedAsync(CreateProduct(1, "RICE-001", "Basmati Rice", "111", true, true));
        using var client = factory.CreateClient();

        var result = await client.GetFromJsonAsync<PagedResultDto<StockProductListItemDto>>(
            "/api/v1/inventory/stock/products?search=missing");

        Assert.NotNull(result);
        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
        Assert.Equal(0, result.TotalPages);
    }

    [Fact]
    public async Task Search_ExcludesInactiveAndNonStockManagedProducts()
    {
        await using var factory = new InventoryStockApiFactory();
        await factory.SeedAsync(
            CreateProduct(1, "MATCH-1", "Inactive match", "111", false, true),
            CreateProduct(2, "MATCH-2", "Not managed match", "222", true, false));
        using var client = factory.CreateClient();

        var result = await client.GetFromJsonAsync<PagedResultDto<StockProductListItemDto>>(
            "/api/v1/inventory/stock/products?search=MATCH");

        Assert.NotNull(result);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task Search_UsesOnlyStockFromAuthorizedStoreAndFranchise()
    {
        await using var factory = new InventoryStockApiFactory();
        await factory.SeedAsync(
            [CreateProduct(1, "RICE-001", "Basmati Rice", "111", true, true)],
            [
                CreateStoreStock(1, 7, 11, 1, 4m),
                CreateStoreStock(2, 7, 12, 1, 99m),
                CreateStoreStock(3, 8, 11, 1, 77m)
            ]);
        using var client = factory.CreateClient();

        var result = await client.GetFromJsonAsync<PagedResultDto<StockProductListItemDto>>(
            "/api/v1/inventory/stock/products?search=RICE");

        Assert.NotNull(result);
        var product = Assert.Single(result.Items);
        Assert.Equal(4m, product.CurrentQuantity);
        Assert.Equal("Low Stock", product.StockStatus);
    }

    [Fact]
    public async Task GetStockProducts_AppliesPaging()
    {
        await using var factory = new InventoryStockApiFactory();
        await factory.SeedAsync(
            CreateProduct(1, "P-1", "Alpha", "111", true, true),
            CreateProduct(2, "P-2", "Beta", "222", true, true),
            CreateProduct(3, "P-3", "Gamma", "333", true, true));
        using var client = factory.CreateClient();

        var result = await client.GetFromJsonAsync<PagedResultDto<StockProductListItemDto>>(
            "/api/v1/inventory/stock/products?pageNumber=2&pageSize=2");

        Assert.NotNull(result);
        Assert.Equal(3, result.TotalCount);
        Assert.Equal(2, result.TotalPages);
        Assert.Equal(2, result.PageNumber);
        Assert.Equal(2, result.PageSize);
        Assert.Equal("Gamma", Assert.Single(result.Items).ProductName);
    }

    private static Product CreateProduct(
        long id,
        string code,
        string name,
        string barcode,
        bool isActive,
        bool isStockManaged)
    {
        var product = (Product)Activator.CreateInstance(typeof(Product), nonPublic: true)!;
        SetProperty(product, nameof(Product.ProductId), id);
        SetProperty(product, nameof(Product.ProductCode), code);
        SetProperty(product, nameof(Product.ProductName), name);
        SetProperty(product, nameof(Product.Barcode), barcode);
        SetProperty(product, nameof(Product.MinimumQuantity), 5L);
        SetProperty(product, nameof(Product.MaximumQuantity), 20L);
        SetProperty(product, nameof(Product.IsActive), isActive);
        SetProperty(product, nameof(Product.IsStockManaged), isStockManaged);
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

internal sealed class InventoryStockApiFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString =
        $"Data Source=stock-products-{Guid.NewGuid():N};Mode=Memory;Cache=Shared;Default Timeout=30";
    private readonly SqliteConnection _anchorConnection;

    public InventoryStockApiFactory()
    {
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
                options.UseSqlite(_connectionString));

            services.RemoveAll<IInternalUserRepository>();
            services.AddScoped<IInternalUserRepository, InventoryStockInternalUserRepository>();

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

    public Task SeedAsync(params Product[] products) => SeedAsync(products, []);

    public async Task SeedAsync(
        IReadOnlyCollection<Product> products,
        IReadOnlyCollection<StoreStock> stocks) =>
        await SeedAsync(products, stocks, []);

    public async Task SeedAsync(
        IReadOnlyCollection<Product> products,
        IReadOnlyCollection<StoreStock> stocks,
        IReadOnlyCollection<ProductBatchStock> batchStocks)
    {
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await dbContext.Database.EnsureCreatedAsync();
        await dbContext.Products.AddRangeAsync(products);
        await dbContext.StoreStocks.AddRangeAsync(stocks);
        await dbContext.ProductBatchStocks.AddRangeAsync(batchStocks);
        await dbContext.SaveChangesAsync();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _anchorConnection.Dispose();
        }
    }
}

internal sealed class InventoryStockInternalUserRepository : IInternalUserRepository
{
    public Task<InternalUserAccountDto?> GetByLoginIdAsync(
        string loginId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<InternalUserAccountDto?>(null);

    public Task<MartUserAccessScopeDto?> GetAccessScopeAsync(
        long userId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<MartUserAccessScopeDto?>(new MartUserAccessScopeDto(7, 11));
}

internal sealed class InventoryStockAuthenticationHandler
    : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string AuthenticationScheme = "InventoryStockTest";

    public InventoryStockAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "41")],
            AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, AuthenticationScheme);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
