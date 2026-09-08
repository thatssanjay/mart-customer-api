using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Mart.Customer.Application.Payments.Dtos;
using Mart.Customer.Domain.Carts;
using Mart.Customer.Persistence;
using Mart.Customer.Shared.Auth;
using Mart.Customer.Tests.Carts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Mart.Customer.Tests.Payments;

public sealed class WalletPaymentQrValidationTests : IClassFixture<GetCartsAuthenticationFactory>
{
    private readonly GetCartsAuthenticationFactory _factory;

    public WalletPaymentQrValidationTests(GetCartsAuthenticationFactory factory) => _factory = factory;

    [Fact]
    public async Task MatchingQrCustomerCartAndToken_ReturnsPaymentCart()
    {
        var payment = await SeedPaymentAsync();
        using var client = CreateCustomerClient(501);

        using var response = await client.GetAsync(Route(payment.Token, payment.CartNumber, 501));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<WalletPaymentCartDto>();
        Assert.NotNull(result);
        Assert.Equal(501, result.CustomerId);
        Assert.Equal(payment.CartNumber, result.CartNumber);
    }

    [Fact]
    public async Task QrCustomerDifferentFromAuthenticatedCustomer_IsRejected()
    {
        var payment = await SeedPaymentAsync();
        using var client = CreateCustomerClient(501);

        using var response = await client.GetAsync(Route(payment.Token, payment.CartNumber, 502));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AuthenticatedCustomerDifferentFromQrAndCartOwner_IsRejected()
    {
        var payment = await SeedPaymentAsync();
        using var client = CreateCustomerClient(502);

        using var response = await client.GetAsync(Route(payment.Token, payment.CartNumber, 501));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task QrCartNumberDifferentFromTokenCart_IsRejected()
    {
        var payment = await SeedPaymentAsync();
        using var client = CreateCustomerClient(501);

        using var response = await client.GetAsync(Route(payment.Token, "OTHER-CART", 501));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task InvalidPaymentToken_IsRejected()
    {
        var payment = await SeedPaymentAsync();
        using var client = CreateCustomerClient(501);
        var invalidToken = $"{payment.CartId}.different-secret";

        using var response = await client.GetAsync(Route(invalidToken, payment.CartNumber, 501));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task MissingQrCustomerAndCartNumber_IsRejected()
    {
        var payment = await SeedPaymentAsync();
        using var client = CreateCustomerClient(501);

        using var response = await client.GetAsync(
            $"/api/v1/wallet-payments/{Uri.EscapeDataString(payment.Token)}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task<(long CartId, string CartNumber, string Token)> SeedPaymentAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var cart = CustomerCart.Create(501, 7, 11, $"QR-{Guid.NewGuid():N}", 41);
        db.CustomerCarts.Add(cart);
        await db.SaveChangesAsync();

        var token = $"{cart.CustomerCartId}.{Guid.NewGuid():N}";
        cart.BeginWalletPaymentAttempt(
            $"WLT-{Guid.NewGuid():N}",
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))),
            100m,
            DateTime.UtcNow.AddMinutes(5));
        await db.SaveChangesAsync();
        return (cart.CustomerCartId, cart.CartNumber, token);
    }

    private HttpClient CreateCustomerClient(long customerId)
    {
        var client = _factory.CreateClient();
        var token = new JwtSecurityToken(
            "customer-tests",
            "cart-tests",
            [
                new Claim(MartTokenClaims.UserId, customerId.ToString()),
                new Claim(MartTokenClaims.LoginType, "customer")
            ],
            DateTime.UtcNow.AddMinutes(-1),
            DateTime.UtcNow.AddMinutes(10),
            new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(GetCartsAuthenticationFactory.CustomerKey)),
                SecurityAlgorithms.HmacSha256));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            new JwtSecurityTokenHandler().WriteToken(token));
        return client;
    }

    private static string Route(string token, string cartNumber, long customerId) =>
        $"/api/v1/wallet-payments/{Uri.EscapeDataString(token)}" +
        $"?cartNumber={Uri.EscapeDataString(cartNumber)}&customerId={customerId}";
}
