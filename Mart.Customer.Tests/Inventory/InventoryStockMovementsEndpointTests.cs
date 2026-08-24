using System.Net.Http.Json;
using System.Reflection;
using Mart.Customer.Application.Customers.Dtos;
using Mart.Customer.Application.Inventory.Dtos;
using Mart.Customer.Domain.Inventory;
using Mart.Customer.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Mart.Customer.Tests.Inventory;

public sealed class InventoryStockMovementsEndpointTests
{
    [Fact]
    public async Task GetStockMovements_AppliesProductMovementTypeAndDateFilters()
    {
        var start = new DateTime(2026, 8, 10, 8, 0, 0, DateTimeKind.Utc);
        await using var factory = new InventoryStockApiFactory();
        await SeedAsync(
            factory,
            [
                CreateProduct(1, "MILK-001", "Whole Milk"),
                CreateProduct(2, "BREAD-001", "Brown Bread")
            ],
            [
                CreateMovement(7, 11, 1, "PURCHASE", start),
                CreateMovement(7, 11, 1, "DAMAGE", start.AddHours(1)),
                CreateMovement(7, 11, 2, "DAMAGE", start.AddHours(2)),
                CreateMovement(7, 11, 1, "DAMAGE", start.AddDays(1))
            ]);
        using var client = factory.CreateClient();

        var byProduct = await client.GetFromJsonAsync<PagedResultDto<StockMovementDto>>(
            "/api/v1/inventory/stock/movements?productId=2");
        var byType = await client.GetFromJsonAsync<PagedResultDto<StockMovementDto>>(
            "/api/v1/inventory/stock/movements?movementType=damage");
        var fromDate = Uri.EscapeDataString(start.AddMinutes(30).ToString("O"));
        var toDate = Uri.EscapeDataString(start.AddHours(1).AddMinutes(30).ToString("O"));
        var byDate = await client.GetFromJsonAsync<PagedResultDto<StockMovementDto>>(
            $"/api/v1/inventory/stock/movements?fromDate={fromDate}&toDate={toDate}");

        Assert.NotNull(byProduct);
        var productMovement = Assert.Single(byProduct.Items);
        Assert.Equal(2, productMovement.ProductId);
        Assert.Equal("BREAD-001", productMovement.ProductCode);
        Assert.Equal("Brown Bread", productMovement.ProductName);

        Assert.NotNull(byType);
        Assert.Equal(3, byType.TotalCount);
        Assert.All(byType.Items, movement => Assert.Equal("DAMAGE", movement.MovementType));

        Assert.NotNull(byDate);
        var datedMovement = Assert.Single(byDate.Items);
        Assert.Equal("DAMAGE", datedMovement.MovementType);
        Assert.Equal(start.AddHours(1), datedMovement.CreatedOn);
    }

    [Fact]
    public async Task GetStockMovements_ReturnsNewestFirstWithPaging()
    {
        var start = new DateTime(2026, 8, 10, 8, 0, 0, DateTimeKind.Utc);
        await using var factory = new InventoryStockApiFactory();
        await SeedAsync(
            factory,
            [CreateProduct(1, "MILK-001", "Whole Milk")],
            [
                CreateMovement(7, 11, 1, "OPENING", start),
                CreateMovement(7, 11, 1, "PURCHASE", start.AddHours(1)),
                CreateMovement(7, 11, 1, "DAMAGE", start.AddHours(2)),
                CreateMovement(7, 11, 1, "SALE_RETURN", start.AddHours(3)),
                CreateMovement(7, 11, 1, "ADJUSTMENT_ADD", start.AddHours(4))
            ]);
        using var client = factory.CreateClient();

        var result = await client.GetFromJsonAsync<PagedResultDto<StockMovementDto>>(
            "/api/v1/inventory/stock/movements?pageNumber=2&pageSize=2");

        Assert.NotNull(result);
        Assert.Equal(5, result.TotalCount);
        Assert.Equal(3, result.TotalPages);
        Assert.Equal(2, result.PageNumber);
        Assert.Equal(2, result.PageSize);
        Assert.Equal(
            ["DAMAGE", "PURCHASE"],
            result.Items.Select(movement => movement.MovementType));
    }

    [Fact]
    public async Task GetStockMovements_ReturnsOnlyAuthorizedStoreAndFranchise()
    {
        var occurredOn = new DateTime(2026, 8, 10, 8, 0, 0, DateTimeKind.Utc);
        await using var factory = new InventoryStockApiFactory();
        await SeedAsync(
            factory,
            [CreateProduct(1, "MILK-001", "Whole Milk")],
            [
                CreateMovement(7, 11, 1, "PURCHASE", occurredOn),
                CreateMovement(7, 12, 1, "DAMAGE", occurredOn.AddHours(1)),
                CreateMovement(8, 11, 1, "OPENING", occurredOn.AddHours(2))
            ]);
        using var client = factory.CreateClient();

        var result = await client.GetFromJsonAsync<PagedResultDto<StockMovementDto>>(
            "/api/v1/inventory/stock/movements");

        Assert.NotNull(result);
        var movement = Assert.Single(result.Items);
        Assert.Equal("PURCHASE", movement.MovementType);
        Assert.Equal(1, result.TotalCount);
    }

    private static async Task SeedAsync(
        InventoryStockApiFactory factory,
        IReadOnlyCollection<Product> products,
        IReadOnlyCollection<StockMovement> movements)
    {
        await factory.SeedAsync(products.ToArray());
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await dbContext.StockMovements.AddRangeAsync(movements);
        await dbContext.SaveChangesAsync();
    }

    private static Product CreateProduct(long id, string code, string name)
    {
        var product = (Product)Activator.CreateInstance(typeof(Product), nonPublic: true)!;
        SetProperty(product, nameof(Product.ProductId), id);
        SetProperty(product, nameof(Product.ProductCode), code);
        SetProperty(product, nameof(Product.ProductName), name);
        SetProperty(product, nameof(Product.IsActive), true);
        SetProperty(product, nameof(Product.IsStockManaged), true);
        return product;
    }

    private static StockMovement CreateMovement(
        long franchiseId,
        long martStoreId,
        long productId,
        string movementType,
        DateTime createdOn) =>
        StockMovement.CreateStockIn(
            franchiseId,
            martStoreId,
            productId,
            movementType,
            "TEST",
            null,
            2m,
            3m,
            5m,
            "Test movement",
            41,
            createdOn);

    private static void SetProperty<TTarget, TValue>(
        TTarget target,
        string propertyName,
        TValue value) =>
        typeof(TTarget)
            .GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public)!
            .SetValue(target, value);
}
