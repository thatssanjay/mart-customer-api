using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Mart.Customer.Application.Carts.Dtos;
using Mart.Customer.Application.Payments.Services;
using Mart.Customer.Domain.Carts;
using Mart.Customer.Domain.Common;
using Mart.Customer.Persistence;
using Mart.Customer.Shared.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Mart.Customer.Tests.Carts;

public sealed class UpdateCartPaymentStatusTests : IClassFixture<GetCartsAuthenticationFactory>
{
    private const string Route = "/api/v1/customer/carts/payment-status";
    private const string Remark = """
        {"reference":"WLT-TEST","status":"EXPIRED","tokenHash":"ABC","amount":313.95,"expiresOn":"2026-09-03T19:18:28.8878419Z","extra":{"values":[1,true,null,"keep"]}}
        """;
    private readonly GetCartsAuthenticationFactory _factory;

    public UpdateCartPaymentStatusTests(GetCartsAuthenticationFactory factory) => _factory = factory;

    [Theory]
    [InlineData("PAID")]
    [InlineData(" paid ")]
    [InlineData("PENDING")]
    [InlineData("EXPIRED")]
    public async Task Owner_UpdatesOnlyJsonStatus_AndPreservesOtherCartFields(string status)
    {
        using var client = CreateClient();
        var cart = await SeedAsync();

        using var response = await client.PatchAsJsonAsync(Route, new { cartId = cart.CustomerCartId, status });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<UpdatedCartPaymentStatusDto>();
        Assert.Equal(cart.CustomerCartId, result!.CartId);
        Assert.Equal(status.Trim().ToUpperInvariant(), result.Status);
        var persisted = await ReadAsync(cart.CustomerCartId);
        using var before = JsonDocument.Parse(Remark);
        using var after = JsonDocument.Parse(persisted.Remarks!);
        Assert.Equal(result.Status, after.RootElement.GetProperty("status").GetString());
        Assert.Equal(before.RootElement.EnumerateObject().Count(), after.RootElement.EnumerateObject().Count());
        foreach (var property in before.RootElement.EnumerateObject().Where(p => p.Name != "status"))
            Assert.Equal(property.Value.GetRawText(), after.RootElement.GetProperty(property.Name).GetRawText());
        Assert.Equal(cart.CartStatus, persisted.CartStatus);
        Assert.Equal(cart.ModifiedOn, persisted.ModifiedOn);
        Assert.Equal(cart.NetAmount, persisted.NetAmount);
        Assert.Equal(cart.PaidOn, persisted.PaidOn);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingOrOtherCustomersCart_ReturnsInvalidCart(bool missing)
    {
        using var client = CreateClient();
        var cart = await SeedAsync(customerId: 502);
        using var response = await client.PatchAsJsonAsync(Route,
            new { cartId = missing ? long.MaxValue : cart.CustomerCartId, status = "PAID", customerId = 502 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Invalid cart.", (await response.Content.ReadFromJsonAsync<ProblemDetails>())!.Detail);
        Assert.Equal(Remark, (await ReadAsync(cart.CustomerCartId)).Remarks);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("FAILED")]
    public async Task InvalidStatus_DoesNotChangeRemark(string? status)
    {
        using var client = CreateClient();
        var cart = await SeedAsync();
        using var response = await client.PatchAsJsonAsync(Route, new { cartId = cart.CustomerCartId, status });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(Remark, (await ReadAsync(cart.CustomerCartId)).Remarks);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("{broken")]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("{\"status\":null}")]
    [InlineData("{\"status\":\"EXPIRED\",\"status\":\"PENDING\"}")]
    public async Task InvalidRemark_IsRejectedWithoutOverwriting(string? remark)
    {
        using var client = CreateClient();
        var cart = await SeedAsync(remark);
        using var response = await client.PatchAsJsonAsync(Route, new { cartId = cart.CustomerCartId, status = "PAID" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(remark, (await ReadAsync(cart.CustomerCartId)).Remarks);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task InvalidCartId_ReturnsBadRequest(long cartId)
    {
        using var client = CreateClient();
        using var response = await client.PatchAsJsonAsync(Route, new { cartId, status = "PAID" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData(null, "501", HttpStatusCode.Unauthorized)]
    [InlineData("internalUser", "501", HttpStatusCode.Forbidden)]
    [InlineData("customer", "0", HttpStatusCode.BadRequest)]
    [InlineData("customer", "invalid", HttpStatusCode.BadRequest)]
    public async Task RequiresCustomerTokenWithValidIdentity(string? loginType, string userId, HttpStatusCode expected)
    {
        using var client = CreateClient(loginType, userId);
        var cart = await SeedAsync();
        using var response = await client.PatchAsJsonAsync(Route, new { cartId = cart.CustomerCartId, status = "PAID" });
        Assert.Equal(expected, response.StatusCode);
        Assert.Equal(Remark, (await ReadAsync(cart.CustomerCartId)).Remarks);
    }

    [Theory]
    [InlineData("PENDING")]
    [InlineData("EXPIRED")]
    [InlineData("PAID")]
    public async Task ExistingCheckout_RequiresPaidStatus(string status)
    {
        using var client = CreateClient();
        var cart = await SeedAsync();
        var token = $"{cart.CustomerCartId}.test-secret";
        cart.BeginWalletPaymentAttempt("WLT-TEST",
            Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(token))),
            313.95m, DateTime.UtcNow.AddMinutes(5));
        cart.UpdatePaymentStatus(status);
        using var scope = _factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IWalletPaymentRequestService>();
        if (status == "PAID")
            Assert.Equal("WLT-TEST", await service.ValidateForCheckoutAsync(cart, token, 313.95m));
        else
            Assert.Equal("The wallet payment has not been paid.",
                (await Assert.ThrowsAsync<DomainException>(() => service.ValidateForCheckoutAsync(cart, token, 313.95m))).Message);
    }

    private HttpClient CreateClient(string? loginType = "customer", string userId = "501")
    {
        var client = _factory.CreateClient();
        if (loginType is null) return client;
        var token = new JwtSecurityToken("customer-tests", "cart-tests",
            [new Claim(MartTokenClaims.UserId, userId), new Claim(MartTokenClaims.LoginType, loginType)],
            DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(10),
            new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(GetCartsAuthenticationFactory.CustomerKey)),
                SecurityAlgorithms.HmacSha256));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
        return client;
    }

    private async Task<CustomerCart> SeedAsync(string? remark = Remark, long customerId = 501)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var cart = CustomerCart.Create(customerId, 7, 11, $"PAY-{Guid.NewGuid():N}", 41);
        db.CustomerCarts.Add(cart);
        db.Entry(cart).Property(c => c.Remarks).CurrentValue = remark;
        await db.SaveChangesAsync();
        return cart;
    }

    private async Task<CustomerCart> ReadAsync(long cartId)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().CustomerCarts
            .AsNoTracking().SingleAsync(c => c.CustomerCartId == cartId);
    }
}
