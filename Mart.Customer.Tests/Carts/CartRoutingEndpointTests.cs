using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Mart.Customer.Application.Abstractions.Data;
using Mart.Customer.Application.Auth.Dtos;
using Mart.Customer.Application.Carts.Commands.CreateCart;
using Mart.Customer.Application.Carts.Dtos;
using Mart.Customer.Application.Inventory.Dtos;
using Mart.Customer.Application.Inventory.Queries.SearchProducts;
using Mart.Customer.Persistence;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Mart.Customer.Tests.Carts;

public sealed class CartRoutingEndpointTests : IClassFixture<CartApiFactory>
{
    private readonly CartApiFactory _factory;

    public CartRoutingEndpointTests(CartApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task MartUserToken_CanAccessInventoryProductsAndCreateCart()
    {
        using var client = _factory.CreateClient();

        using var inventoryResponse = await client.GetAsync(
            "/api/v1/inventory/products",
            CancellationToken.None);
        using var cartResponse = await client.PostAsJsonAsync(
            "/api/v1/inventory/carts",
            new { customerId = 501L },
            CancellationToken.None);

        Assert.True(
            inventoryResponse.StatusCode == HttpStatusCode.OK,
            await inventoryResponse.Content.ReadAsStringAsync(CancellationToken.None));
        Assert.True(
            cartResponse.StatusCode == HttpStatusCode.Created,
            await cartResponse.Content.ReadAsStringAsync(CancellationToken.None));
    }

    [Fact]
    public async Task LegacyCartRoute_IsNotExposed()
    {
        using var client = _factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/v1/carts",
            new { customerId = 501L },
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task InventoryCartRoute_WhenUnauthenticated_ReturnsUnauthorized()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/v1/inventory/carts",
            new { customerId = 501L },
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}

public sealed class CartApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseInMemoryDatabase(Guid.NewGuid().ToString()));

            services.RemoveAll<IInternalUserRepository>();
            services.AddScoped<IInternalUserRepository, CartInternalUserRepository>();

            services.RemoveAll<IRequestHandler<SearchProductsQuery, IReadOnlyList<ProductListItemDto>>>();
            services.AddScoped<
                IRequestHandler<SearchProductsQuery, IReadOnlyList<ProductListItemDto>>,
                CartSearchProductsHandler>();
            services.RemoveAll<IRequestHandler<CreateCartCommand, CreatedCartDto>>();
            services.AddScoped<IRequestHandler<CreateCartCommand, CreatedCartDto>, CartCreateHandler>();

            services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = CartAuthenticationHandler.AuthenticationScheme;
                    options.DefaultChallengeScheme = CartAuthenticationHandler.AuthenticationScheme;
                })
                .AddScheme<AuthenticationSchemeOptions, CartAuthenticationHandler>(
                    CartAuthenticationHandler.AuthenticationScheme,
                    _ => { });
        });
    }
}

internal sealed class CartCreateHandler : IRequestHandler<CreateCartCommand, CreatedCartDto>
{
    public Task<CreatedCartDto> Handle(
        CreateCartCommand request,
        CancellationToken cancellationToken) =>
        Task.FromResult(new CreatedCartDto(1, "CART-TEST"));
}

internal sealed class CartSearchProductsHandler
    : IRequestHandler<SearchProductsQuery, IReadOnlyList<ProductListItemDto>>
{
    public Task<IReadOnlyList<ProductListItemDto>> Handle(
        SearchProductsQuery request,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ProductListItemDto>>([]);
}

internal sealed class CartInternalUserRepository : IInternalUserRepository
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

internal sealed class CartAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string AuthenticationScheme = "CartTest";

    public CartAuthenticationHandler(
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
