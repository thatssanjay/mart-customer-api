using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Mart.Customer.Application.Abstractions.Data;
using Mart.Customer.Application.Auth.Dtos;
using Mart.Customer.Application.Carts.Dtos;
using Mart.Customer.Domain.Carts;
using Mart.Customer.Persistence;
using Mart.Customer.Shared.Auth;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;

namespace Mart.Customer.Tests.Carts;

public sealed class GetCartsAuthenticationTests : IClassFixture<GetCartsAuthenticationFactory>
{
    private readonly GetCartsAuthenticationFactory _factory;

    public GetCartsAuthenticationTests(GetCartsAuthenticationFactory factory) => _factory = factory;

    [Theory]
    [InlineData("")]
    [InlineData("&customerId=501")]
    [InlineData("&customerId=502")]
    [InlineData("&customerId=0")]
    [InlineData("&customerId=-1")]
    public async Task CustomerToken_AlwaysReturnsOnlyTokenOwnersCarts(string query)
    {
        using var client = await _factory.CreateSeededClientAsync();
        SetToken(client, "501", "customer");

        using var response = await client.GetAsync($"/api/v1/customer/carts?cartNumber=OWN-STORE&status=active{query}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var carts = await response.Content.ReadFromJsonAsync<List<CartDetailsDto>>();
        Assert.NotNull(carts);
        Assert.Single(carts);
        Assert.All(carts, cart => Assert.Equal(501L, cart.CustomerId));
        Assert.All(carts, cart => Assert.Equal("Active", cart.CartStatus));
        var item = Assert.Single(carts.Single(cart => cart.CartNumber == "OWN-STORE").Items);
        Assert.Equal("Test product", item.ProductNameSnapshot);
        Assert.Equal(10m, item.LineTotal);
    }

    [Theory]
    [InlineData("internalUser")]
    [InlineData(null)] // Legacy Mart tokens do not always include loginType.
    public async Task MartToken_RetainsRequestedCustomerAndDatabaseStoreScope(string? loginType)
    {
        using var client = await _factory.CreateSeededClientAsync();
        SetToken(client, "41", loginType, martToken: true);

        using var response = await client.GetAsync("/api/v1/inventory/carts?customerId=502&cartStatus=Active");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var carts = await response.Content.ReadFromJsonAsync<List<CartDetailsDto>>();
        var cart = Assert.Single(carts!);
        Assert.Equal(502L, cart.CustomerId);
        Assert.Equal(7L, cart.FranchiseId);
        Assert.Equal(11L, cart.MartStoreId);
    }

    [Fact]
    public async Task MartToken_WithoutAssignment_RetainsExistingRejection()
    {
        using var client = await _factory.CreateSeededClientAsync();
        SetToken(client, "99", "internalUser", martToken: true);

        using var response = await client.GetAsync("/api/v1/inventory/carts?customerId=501&cartStatus=Active");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("active franchise and store assignment", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("invalid")]
    [InlineData("0")]
    [InlineData("-1")]
    public async Task CustomerToken_WithoutValidIdentity_CannotUseRequestCustomerId(string? userId)
    {
        using var client = await _factory.CreateSeededClientAsync();
        SetToken(client, userId, "customer");

        using var response = await client.GetAsync("/api/v1/customer/carts?customerId=502&cartNumber=OWN-STORE&status=active");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("valid UserId claim", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("", "cartNumber")]
    [InlineData("status=active", "cartNumber")]
    [InlineData("cartNumber=&status=active", "cartNumber")]
    [InlineData("cartNumber=%20&status=active", "cartNumber")]
    [InlineData("cartNumber=OWN-STORE", "status")]
    [InlineData("cartNumber=OWN-STORE&status=", "status")]
    [InlineData("cartNumber=OWN-STORE&status=%20", "status")]
    [InlineData("cartNumber=OWN-STORE&cartStatus=active", "status")]
    public async Task CustomerEndpoint_RequiresCartNumberAndStatus(string query, string field)
    {
        using var client = await _factory.CreateSeededClientAsync();
        SetToken(client, "501", "customer");

        using var response = await client.GetAsync($"/api/v1/customer/carts?{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Contains(field, problem!.Errors.Keys);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("expired")]
    [InlineData("bad-signature")]
    public async Task InvalidAuthentication_ReturnsUnauthorized(string tokenKind)
    {
        using var client = await _factory.CreateSeededClientAsync();
        if (tokenKind != "missing")
        {
            SetToken(client, "501", "customer", expired: tokenKind == "expired",
                signingKey: tokenKind == "bad-signature" ? "an-untrusted-signing-key-at-least-32-characters" : null);
        }

        using var response = await client.GetAsync("/api/v1/customer/carts?cartNumber=OWN-STORE&status=active");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("internalUser")]
    [InlineData(null)]
    [InlineData("unknown")]
    public async Task CustomerEndpoint_RejectsNonCustomerTokens(string? loginType)
    {
        using var client = await _factory.CreateSeededClientAsync();
        SetToken(client, "41", loginType, martToken: true);

        using var response = await client.GetAsync("/api/v1/customer/carts?cartNumber=OWN-STORE&status=active");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("OTHER-CUSTOMER", "active")]
    [InlineData("DOES-NOT-EXIST", "active")]
    [InlineData("OWN", "active")]
    [InlineData("OWN-STORE", "cancelled")]
    [InlineData("CANCELLED", "active")]
    [InlineData("OWN-STORE", "unknown")]
    public async Task InvalidCart_ReturnsSameValidationWithoutDisclosingOwnership(string cartNumber, string status)
    {
        using var client = await _factory.CreateSeededClientAsync();
        SetToken(client, "501", "customer");

        using var response = await client.GetAsync(
            $"/api/v1/customer/carts?cartNumber={cartNumber}&status={status}&customerId=502");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("Invalid cart number.", problem!.Detail);
        Assert.Equal("Domain rule violation", problem.Title);
    }

    [Theory]
    [InlineData("OWN-OTHER-STORE", "ACTIVE", "Active")]
    [InlineData("CANCELLED", "cancelled", "Cancelled")]
    [InlineData("%20OWN-STORE%20", "%20active%20", "Active")]
    public async Task CustomerEndpoint_ReturnsOnlyExactMatchingCart(string cartNumber, string status, string expectedStatus)
    {
        using var client = await _factory.CreateSeededClientAsync();
        SetToken(client, "501", "customer");

        using var response = await client.GetAsync($"/api/v1/customer/carts?cartNumber={cartNumber}&status={status}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var carts = await response.Content.ReadFromJsonAsync<List<CartDetailsDto>>();
        var cart = Assert.Single(carts!);
        Assert.Equal(Uri.UnescapeDataString(cartNumber).Trim(), cart.CartNumber);
        Assert.Equal(expectedStatus, cart.CartStatus);
        Assert.Equal(501L, cart.CustomerId);
    }

    [Theory]
    [InlineData("cartNumber", 51)]
    [InlineData("status", 31)]
    public async Task CustomerEndpoint_RejectsOverlongParameters(string field, int length)
    {
        using var client = await _factory.CreateSeededClientAsync();
        SetToken(client, "501", "customer");
        var value = new string('x', length);
        var query = field == "cartNumber" ? $"cartNumber={value}&status=active" : $"cartNumber=OWN-STORE&status={value}";

        using var response = await client.GetAsync($"/api/v1/customer/carts?{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Contains(field, problem!.Errors.Keys);
    }

    [Theory]
    [InlineData("cartStatus=Active", HttpStatusCode.BadRequest)]
    [InlineData("customerId=999&cartStatus=Active", HttpStatusCode.OK)]
    public async Task InventoryEndpoint_RetainsExistingValidationAndEmptyResults(string query, HttpStatusCode expected)
    {
        using var client = await _factory.CreateSeededClientAsync();
        SetToken(client, "41", "internalUser", martToken: true);

        using var response = await client.GetAsync($"/api/v1/inventory/carts?{query}");

        Assert.Equal(expected, response.StatusCode);
        if (expected == HttpStatusCode.OK)
            Assert.Empty((await response.Content.ReadFromJsonAsync<List<CartDetailsDto>>())!);
    }

    private static void SetToken(HttpClient client, string? userId, string? loginType,
        bool martToken = false, bool expired = false, string? signingKey = null)
    {
        var claims = new List<Claim>();
        if (userId is not null)
            claims.Add(new Claim(MartTokenClaims.UserId, userId));
        if (loginType is not null)
            claims.Add(new Claim(MartTokenClaims.LoginType, loginType));
        // Store claims must not override the Mart user's database assignment.
        claims.Add(new Claim(MartTokenClaims.FranchiseId, "999"));
        claims.Add(new Claim(MartTokenClaims.StoreId, "999"));
        var token = new JwtSecurityToken(
            martToken ? "mart-tests" : "customer-tests", "cart-tests", claims,
            DateTime.UtcNow.AddHours(-2),
            expired ? DateTime.UtcNow.AddHours(-1) : DateTime.UtcNow.AddHours(1),
            new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
                signingKey ?? (martToken ? GetCartsAuthenticationFactory.MartKey : GetCartsAuthenticationFactory.CustomerKey))),
                SecurityAlgorithms.HmacSha256));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", new JwtSecurityTokenHandler().WriteToken(token));
    }
}

public sealed class GetCartsAuthenticationFactory : WebApplicationFactory<Program>
{
    public const string CustomerKey = "customer-test-signing-key-at-least-32-characters";
    public const string MartKey = "mart-test-signing-key-at-least-32-characters";
    private readonly string _databaseName = Guid.NewGuid().ToString();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = "customer-tests",
                ["Jwt:Audience"] = "cart-tests",
                ["Jwt:SigningKey"] = CustomerKey,
                ["Jwt:MartIssuer"] = "mart-tests",
                ["Jwt:MartAudience"] = "cart-tests",
                ["Jwt:MartSigningKey"] = MartKey
            }));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();
            services.AddDbContext<ApplicationDbContext>(options => options.UseInMemoryDatabase(_databaseName));
            services.RemoveAll<IInternalUserRepository>();
            services.AddScoped<IInternalUserRepository, GetCartsInternalUserRepository>();
        });
    }

    public async Task<HttpClient> CreateSeededClientAsync()
    {
        var client = CreateClient();
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        if (!await db.CustomerCarts.AnyAsync())
        {
            var own = CustomerCart.Create(501, 7, 11, "OWN-STORE", 41);
            own.AddItem(1, "Test product", 1, 10, 10, 0, 0, 41);
            var cancelled = CustomerCart.Create(501, 7, 11, "CANCELLED", 41);
            cancelled.ChangeStatus("Cancelled");
            db.CustomerCarts.AddRange(own, cancelled,
                CustomerCart.Create(501, 8, 12, "OWN-OTHER-STORE", 41),
                CustomerCart.Create(502, 7, 11, "OTHER-CUSTOMER", 41),
                CustomerCart.Create(502, 7, 12, "OTHER-STORE", 41),
                CustomerCart.Create(502, 8, 11, "OTHER-FRANCHISE", 41));
            await db.SaveChangesAsync();
        }
        return client;
    }
}

internal sealed class GetCartsInternalUserRepository : IInternalUserRepository
{
    public Task<InternalUserAccountDto?> GetByLoginIdAsync(string loginId,
        CancellationToken cancellationToken = default) => Task.FromResult<InternalUserAccountDto?>(null);

    public Task<MartUserAccessScopeDto?> GetAccessScopeAsync(long userId,
        CancellationToken cancellationToken = default) =>
        // An overlapping internal user ID must never turn a customer token into staff access.
        Task.FromResult<MartUserAccessScopeDto?>(userId is 41 or 501 ? new(7, 11) : null);
}
