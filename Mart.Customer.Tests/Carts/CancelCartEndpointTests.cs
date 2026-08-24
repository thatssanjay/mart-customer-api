using System.Net;
using System.Net.Http.Json;
using Mart.Customer.Application.Carts.Dtos;
using Mart.Customer.Domain.Carts;
using Mart.Customer.Domain.Orders;
using Mart.Customer.Persistence;
using Mart.Customer.Tests.Orders;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Mart.Customer.Tests.Carts;

public sealed class CancelCartEndpointTests : IClassFixture<OrderDetailApiFactory>
{
    private readonly OrderDetailApiFactory _factory;

    public CancelCartEndpointTests(OrderDetailApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CancelCart_WhenCartIsActive_CancelsCartAndOnlyUpdatesCancellationFields()
    {
        var cart = await SeedCartAsync();
        var createdOn = cart.CreatedOn;
        var modifiedOn = cart.ModifiedOn;
        using var client = _factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            $"/api/v1/carts/{cart.CustomerCartId}/cancel",
            new { remarks = "  Bill reset by cashier.  " });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<CancelledCartDto>();
        Assert.NotNull(result);
        Assert.Equal(cart.CustomerCartId, result.CustomerCartId);
        Assert.Equal("Cancelled", result.CartStatus);
        Assert.Equal("Bill reset by cashier.", result.Remarks);

        var persisted = await GetCartAsync(cart.CustomerCartId);
        Assert.Equal("Cancelled", persisted.CartStatus);
        Assert.NotNull(persisted.CancelledOn);
        Assert.Equal("Bill reset by cashier.", persisted.Remarks);
        Assert.Equal(createdOn, persisted.CreatedOn);
        Assert.Equal(modifiedOn, persisted.ModifiedOn);
        Assert.Null(persisted.PaidOn);
        Assert.Null(persisted.CustomerApprovedOn);
    }

    [Fact]
    public async Task CancelCart_WhenCartIsPaid_IsRejectedWithoutChangingCart()
    {
        var paidOn = DateTime.UtcNow.AddMinutes(-5);
        var cart = CustomerCart.Create(
            501,
            OrderDetailInternalUserRepository.FranchiseId,
            OrderDetailInternalUserRepository.StoreId,
            UniqueCartNumber(),
            41);
        cart.MarkPaid(0, paidOn);
        await SeedCartAsync(cart);
        using var client = _factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            $"/api/v1/carts/{cart.CustomerCartId}/cancel",
            new { remarks = "Should not apply" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("Only an active unpaid cart can be cancelled.", problem?.Detail);
        var persisted = await GetCartAsync(cart.CustomerCartId);
        Assert.Equal("Paid", persisted.CartStatus);
        Assert.Equal(paidOn, persisted.PaidOn);
        Assert.Null(persisted.CancelledOn);
        Assert.Null(persisted.Remarks);
    }

    [Fact]
    public async Task CancelCart_WhenCartIsAlreadyCancelled_IsRejectedAndOriginalCancellationIsPreserved()
    {
        var originalCancelledOn = DateTime.UtcNow.AddMinutes(-10);
        var cart = CustomerCart.Create(
            501,
            OrderDetailInternalUserRepository.FranchiseId,
            OrderDetailInternalUserRepository.StoreId,
            UniqueCartNumber(),
            41);
        cart.Cancel("Original cancellation", originalCancelledOn);
        await SeedCartAsync(cart);
        using var client = _factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            $"/api/v1/carts/{cart.CustomerCartId}/cancel",
            new { remarks = "Replacement cancellation" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("Only an active unpaid cart can be cancelled.", problem?.Detail);
        var persisted = await GetCartAsync(cart.CustomerCartId);
        Assert.Equal(originalCancelledOn, persisted.CancelledOn);
        Assert.Equal("Original cancellation", persisted.Remarks);
    }

    [Fact]
    public async Task CancelCart_WhenCartIsMissing_ReturnsNotFound()
    {
        using var client = _factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/v1/carts/9223372036854775806/cancel",
            new { remarks = "Missing" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CancelCart_WhenCartHasExistingOrder_IsRejectedWithoutChangingCart()
    {
        var cart = await SeedCartAsync();
        await SeedOrderAsync(cart);
        using var client = _factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            $"/api/v1/carts/{cart.CustomerCartId}/cancel",
            new { remarks = "Should not apply" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("A cart with an existing order cannot be cancelled.", problem?.Detail);
        var persisted = await GetCartAsync(cart.CustomerCartId);
        Assert.Equal("Active", persisted.CartStatus);
        Assert.Null(persisted.CancelledOn);
        Assert.Null(persisted.Remarks);
    }

    private Task<CustomerCart> SeedCartAsync() => SeedCartAsync(CustomerCart.Create(
        501,
        OrderDetailInternalUserRepository.FranchiseId,
        OrderDetailInternalUserRepository.StoreId,
        UniqueCartNumber(),
        41));

    private async Task<CustomerCart> SeedCartAsync(CustomerCart cart)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        dbContext.CustomerCarts.Add(cart);
        await dbContext.SaveChangesAsync();
        return cart;
    }

    private async Task SeedOrderAsync(CustomerCart cart)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        dbContext.CustomerOrders.Add(CustomerOrder.Create(
            cart.CustomerCartId,
            $"INV-{Guid.NewGuid():N}",
            cart.CustomerId,
            cart.FranchiseId,
            cart.MartStoreId,
            DateTime.UtcNow,
            cart.TotalItemCount,
            cart.GrossAmount,
            cart.DiscountAmount,
            cart.GSTAmount,
            cart.NetAmount,
            null,
            0,
            cart.FinalPayableAmount,
            41));
        await dbContext.SaveChangesAsync();
    }

    private async Task<CustomerCart> GetCartAsync(long customerCartId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await dbContext.CustomerCarts
            .AsNoTracking()
            .SingleAsync(cart => cart.CustomerCartId == customerCartId);
    }

    private static string UniqueCartNumber() => $"CART-CANCEL-{Guid.NewGuid():N}";
}
