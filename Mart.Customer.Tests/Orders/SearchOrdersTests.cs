using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Mart.Customer.Application.Customers.Dtos;
using Mart.Customer.Application.Orders.Dtos;
using Mart.Customer.Domain.Orders;
using Mart.Customer.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Mart.Customer.Tests.Orders;

public sealed class SearchOrdersEndpointTests : IClassFixture<OrderDetailApiFactory>
{
    private readonly OrderDetailApiFactory _factory;

    public SearchOrdersEndpointTests(OrderDetailApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task SearchOrders_FiltersBySnapshotCustomerName()
    {
        var expected = await SeedOrderAsync("INV-SEARCH-NAME", "Anita Snapshot", "9000000001", Utc(1));
        await SeedOrderAsync("INV-SEARCH-OTHER", "Bharat Snapshot", "9000000002", Utc(1));

        var result = await SearchAsync("customerName=anita");

        Assert.Equal(expected.CustomerOrderId, Assert.Single(result.Items).CustomerOrderId);
    }

    [Fact]
    public async Task SearchOrders_FiltersBySnapshotMobileNumber()
    {
        var expected = await SeedOrderAsync("INV-SEARCH-MOBILE", "Mobile Customer", "9000000003", Utc(2));
        await SeedOrderAsync("INV-SEARCH-MOBILE-OTHER", "Mobile Customer", "9000000004", Utc(2));

        var result = await SearchAsync("mobileNumber=9000000003");

        Assert.Equal(expected.CustomerOrderId, Assert.Single(result.Items).CustomerOrderId);
    }

    [Fact]
    public async Task SearchOrders_FiltersByInvoiceNumber()
    {
        var expected = await SeedOrderAsync("INV-SEARCH-INVOICE-123", "Invoice Customer", "9000000005", Utc(3));
        await SeedOrderAsync("INV-SEARCH-INVOICE-456", "Invoice Customer", "9000000005", Utc(3));

        var result = await SearchAsync("invoiceNumber=123");

        Assert.Equal(expected.CustomerOrderId, Assert.Single(result.Items).CustomerOrderId);
    }

    [Fact]
    public async Task SearchOrders_CombinesSuppliedFiltersWithAnd()
    {
        var expected = await SeedOrderAsync("INV-COMBINED-TARGET", "Combined Customer", "9000000007", Utc(8));
        await SeedOrderAsync("INV-COMBINED-OTHER", "Combined Customer", "9000000007", Utc(8));
        await SeedOrderAsync("INV-COMBINED-TARGET-OTHER", "Other Customer", "9000000007", Utc(8));

        var result = await SearchAsync(
            "customerName=combined&mobileNumber=9000000007&invoiceNumber=TARGET");

        Assert.Equal(expected.CustomerOrderId, Assert.Single(result.Items).CustomerOrderId);
    }

    [Fact]
    public async Task SearchOrders_WhenThereAreNoMatches_ReturnsEmptyPage()
    {
        var result = await SearchAsync("invoiceNumber=INV-NOT-FOUND");

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
        Assert.Equal(0, result.TotalPages);
    }

    [Fact]
    public async Task SearchOrders_PagesNewestFirstUsingOrderIdAsTieBreaker()
    {
        var first = await SeedOrderAsync("INV-PAGE-ONE", "Paging Customer", "9000000008", Utc(9));
        var second = await SeedOrderAsync("INV-PAGE-TWO", "Paging Customer", "9000000008", Utc(9));
        var latest = await SeedOrderAsync("INV-PAGE-LATEST", "Paging Customer", "9000000008", Utc(10));

        var result = await SearchAsync("customerName=paging&pageNumber=2&pageSize=2");

        Assert.Equal(3, result.TotalCount);
        Assert.Equal(2, result.TotalPages);
        Assert.Equal(new[] { first.CustomerOrderId }, result.Items.Select(item => item.CustomerOrderId));
        Assert.True(latest.CustomerOrderId > second.CustomerOrderId);
    }

    [Fact]
    public async Task SearchOrders_ExcludesOrdersOutsideCashierFranchiseAndStore()
    {
        await SeedOrderAsync("INV-AUTHORIZED", "Authorized Customer", "9000000009", Utc(11));
        var unauthorizedStore = await SeedOrderAsync(
            "INV-UNAUTHORIZED-STORE", "Authorized Customer", "9000000009", Utc(12), martStoreId: 12);
        var unauthorizedFranchise = await SeedOrderAsync(
            "INV-UNAUTHORIZED-FRANCHISE", "Authorized Customer", "9000000009", Utc(13), franchiseId: 8);

        var result = await SearchAsync("customerName=authorized");

        Assert.DoesNotContain(result.Items, item => item.CustomerOrderId == unauthorizedStore.CustomerOrderId);
        Assert.DoesNotContain(result.Items, item => item.CustomerOrderId == unauthorizedFranchise.CustomerOrderId);
        Assert.Single(result.Items);
    }

    [Fact]
    public async Task SearchOrders_RejectsInvalidPaging()
    {
        using var client = _factory.CreateClient();
        using var pagingResponse = await client.GetAsync("/api/v1/orders/search?pageNumber=0&pageSize=101");

        Assert.Equal(HttpStatusCode.BadRequest, pagingResponse.StatusCode);
        Assert.Equal("Validation failed", (await pagingResponse.Content.ReadFromJsonAsync<ValidationProblemDetails>())!.Title);
    }

    [Fact]
    public async Task SearchOrders_ReturnsGridFieldsOnly()
    {
        await SeedOrderAsync("INV-GRID", "Grid Customer", "9000000010", Utc(13));
        using var client = _factory.CreateClient();
        using var response = await client.GetAsync("/api/v1/orders/search?invoiceNumber=INV-GRID");
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var item = json.RootElement.GetProperty("items")[0];

        Assert.False(item.TryGetProperty("items", out _));
        Assert.False(item.TryGetProperty("payments", out _));
        Assert.False(item.TryGetProperty("invoiceArchivePath", out _));
    }

    private async Task<PagedResultDto<OrderSearchItemDto>> SearchAsync(string query)
    {
        using var client = _factory.CreateClient();
        using var response = await client.GetAsync($"/api/v1/orders/search?{query}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<PagedResultDto<OrderSearchItemDto>>())!;
    }

    private async Task<CustomerOrder> SeedOrderAsync(
        string invoiceNumber,
        string customerName,
        string mobileNumber,
        DateTime invoiceDate,
        long martStoreId = OrderDetailInternalUserRepository.StoreId,
        long franchiseId = OrderDetailInternalUserRepository.FranchiseId)
    {
        var uniqueId = Random.Shared.NextInt64(10_000_000_000, 90_000_000_000);
        var order = CustomerOrder.Create(
            uniqueId,
            invoiceNumber,
            uniqueId,
            franchiseId,
            martStoreId,
            invoiceDate,
            1,
            110m,
            10m,
            18m,
            118m,
            null,
            0m,
            118m,
            41,
            customerNameSnapshot: customerName,
            customerMobileSnapshot: mobileNumber);
        order.AddItem(uniqueId, 501, "Snapshot product", 1m, 100m, 110m, 110m, 10m, 18m, 18m, 118m);
        order.AddPayment("Cash", 118m, null);

        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        dbContext.CustomerOrders.Add(order);
        await dbContext.SaveChangesAsync();
        return order;
    }

    private static DateTime Utc(int day) => new(2026, 8, day, 10, 0, 0, DateTimeKind.Utc);
}
