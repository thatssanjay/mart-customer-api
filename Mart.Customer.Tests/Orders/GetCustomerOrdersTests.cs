using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Mart.Customer.Application.Customers.Dtos;
using Mart.Customer.Application.Orders.Dtos;
using Mart.Customer.Domain.Orders;
using Mart.Customer.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace Mart.Customer.Tests.Orders;

public sealed class GetCustomerOrdersEndpointTests : IClassFixture<OrderDetailApiFactory>
{
    private readonly OrderDetailApiFactory _factory;

    public GetCustomerOrdersEndpointTests(OrderDetailApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetCustomerOrders_ReturnsRequestedPageNewestFirst()
    {
        const long customerId = 8101;
        var oldest = await SeedOrderAsync(
            customerId,
            new DateTime(2026, 8, 1, 10, 0, 0, DateTimeKind.Utc),
            archiveInvoice: true);
        await SeedOrderAsync(customerId, new DateTime(2026, 8, 2, 10, 0, 0, DateTimeKind.Utc));
        await SeedOrderAsync(customerId, new DateTime(2026, 8, 3, 10, 0, 0, DateTimeKind.Utc));
        await SeedOrderAsync(customerId + 1, new DateTime(2026, 8, 4, 10, 0, 0, DateTimeKind.Utc));
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync(
            $"/api/v1/customers/{customerId}/orders?pageNumber=2&pageSize=2",
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<PagedResultDto<OrderHistoryItemDto>>(
            CancellationToken.None);
        Assert.NotNull(result);
        Assert.Equal(2, result.PageNumber);
        Assert.Equal(2, result.PageSize);
        Assert.Equal(3, result.TotalCount);
        Assert.Equal(2, result.TotalPages);
        var order = Assert.Single(result.Items);
        Assert.Equal(oldest.CustomerOrderId, order.CustomerOrderId);
        Assert.True(order.IsInvoiceDocumentAvailable);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(CancellationToken.None));
        var item = json.RootElement.GetProperty("items")[0];
        Assert.False(item.TryGetProperty("items", out _));
        Assert.False(item.TryGetProperty("payments", out _));
        Assert.False(item.TryGetProperty("invoiceArchivePath", out _));
    }

    [Fact]
    public async Task GetCustomerOrders_WhenCustomerHasNoOrders_ReturnsEmptyPage()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync(
            "/api/v1/customers/8201/orders",
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<PagedResultDto<OrderHistoryItemDto>>(
            CancellationToken.None);
        Assert.NotNull(result);
        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
        Assert.Equal(0, result.TotalPages);
    }

    [Fact]
    public async Task GetCustomerOrders_WhenMobileCustomerDoesNotOwnCustomerId_ReturnsForbidden()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(OrderDetailAuthenticationHandler.CustomerIdHeader, "8301");

        using var response = await client.GetAsync(
            "/api/v1/customers/8302/orders",
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetCustomerOrders_WhenPageSizeExceedsCap_ReturnsValidationProblem()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync(
            "/api/v1/customers/8401/orders?pageSize=101",
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(
            CancellationToken.None);
        Assert.NotNull(problem);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.Status);
        Assert.Equal("Validation failed", problem.Title);
    }

    private async Task<CustomerOrder> SeedOrderAsync(
        long customerId,
        DateTime orderDate,
        bool archiveInvoice = false)
    {
        var uniqueId = Random.Shared.NextInt64(10_000_000_000, 90_000_000_000);
        var order = CustomerOrder.Create(
            uniqueId,
            $"INV-HISTORY-{uniqueId}",
            customerId,
            OrderDetailInternalUserRepository.FranchiseId,
            OrderDetailInternalUserRepository.StoreId,
            orderDate,
            1,
            110m,
            10m,
            18m,
            118m,
            null,
            0m,
            118m,
            41);
        order.AddItem(
            uniqueId,
            501,
            "History snapshot product",
            1m,
            100m,
            110m,
            110m,
            10m,
            18m,
            18m,
            118m);
        order.AddPayment("Cash", 118m, null);

        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        dbContext.CustomerOrders.Add(order);
        await dbContext.SaveChangesAsync(CancellationToken.None);
        if (archiveInvoice)
        {
            dbContext.CustomerOrderInvoiceDocuments.Add(CustomerOrderInvoiceDocument.Create(
                order.CustomerOrderId,
                order.InvoiceTemplateVersion,
                $"private/invoices/{uniqueId}.pdf",
                new string('A', 64),
                1,
                DateTime.UtcNow));
            await dbContext.SaveChangesAsync(CancellationToken.None);
        }
        return order;
    }
}
