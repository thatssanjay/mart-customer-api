using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Mart.Customer.Application.Orders.Dtos;
using Mart.Customer.Domain.Orders;
using Mart.Customer.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Mart.Customer.Tests.Orders;

public sealed class VerifyOrderEndpointTests : IClassFixture<OrderDetailApiFactory>
{
    private readonly OrderDetailApiFactory _factory;

    public VerifyOrderEndpointTests(OrderDetailApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Verify_WhenTokenIsValid_ReturnsMinimalVerification()
    {
        var verificationCode = Guid.NewGuid().ToString("N");
        var order = await SeedOrderAsync(verificationCode);
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(
            OrderDetailAuthenticationHandler.UnauthenticatedHeader,
            "true");

        using var response = await client.GetAsync(
            $"/api/v1/orders/verify/{verificationCode}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<OrderVerificationDto>();
        Assert.NotNull(result);
        Assert.Equal(order.InvoiceNumber, result.InvoiceNumber);
        Assert.Equal(order.OrderDate, result.InvoiceDate);
        Assert.Equal(order.MartStoreId, result.Store);
        Assert.Equal(order.FinalPayableAmount, result.Amount);
        Assert.Equal(order.OrderStatus, result.Status);
        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Verify_WhenTokenIsInvalid_ReturnsNotFound()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync(
            $"/api/v1/orders/verify/{Guid.NewGuid():N}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Verify_ResponseContainsOnlyApprovedPublicFields()
    {
        var verificationCode = Guid.NewGuid().ToString("N");
        await SeedOrderAsync(verificationCode, includeSensitiveSnapshots: true);
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync(
            $"/api/v1/orders/verify/{verificationCode}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var fields = json.RootElement
            .EnumerateObject()
            .Select(property => property.Name)
            .OrderBy(name => name)
            .ToArray();
        Assert.Equal(
            new[] { "amount", "invoiceDate", "invoiceNumber", "isValid", "status", "store" },
            fields);
    }

    private async Task<CustomerOrder> SeedOrderAsync(
        string verificationCode,
        bool includeSensitiveSnapshots = false)
    {
        var uniqueId = Random.Shared.NextInt64(10_000_000_000, 90_000_000_000);
        var order = CustomerOrder.Create(
            uniqueId,
            $"INV-VERIFY-{uniqueId}",
            987654321,
            OrderDetailInternalUserRepository.FranchiseId,
            OrderDetailInternalUserRepository.StoreId,
            new DateTime(2026, 8, 24, 8, 15, 0, DateTimeKind.Utc),
            1,
            150m,
            10m,
            25.20m,
            165.20m,
            null,
            0m,
            165.20m,
            41,
            verificationCode: verificationCode);

        if (includeSensitiveSnapshots)
        {
            order.AddItem(
                uniqueId,
                777,
                "Private item snapshot",
                1m,
                150m,
                150m,
                150m,
                10m,
                18m,
                25.20m,
                165.20m);
            order.AddPayment("Card", 165.20m, "private-payment-reference");
            order.MarkInvoiceArchived("private/invoices/secret.pdf");
        }

        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        dbContext.CustomerOrders.Add(order);
        await dbContext.SaveChangesAsync();
        return order;
    }
}
