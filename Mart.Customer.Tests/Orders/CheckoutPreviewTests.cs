using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Mart.Customer.Application.Abstractions.Data;
using Mart.Customer.Application.Auth.Dtos;
using Mart.Customer.Application.Orders.Dtos;
using Mart.Customer.Application.Orders.Queries.GetOrderCheckoutPreview;
using Mart.Customer.Application.Orders.Services;
using Mart.Customer.Application.Wallets.Services;
using Mart.Customer.Domain.Carts;
using Mart.Customer.Domain.Common;
using Mart.Customer.Persistence;
using Mart.Customer.Tests.Wallets;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Mart.Customer.Tests.Orders;

public sealed class CheckoutPreviewServiceTests
{
    [Fact]
    public async Task Handle_WhenCartAndRedemptionAreValid_ReturnsTrustedTotalsWithoutTrackingOrWriting()
    {
        await using var scope = CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var cart = CreateCart("CART-PREVIEW-VALID", withItem: true);
        dbContext.CustomerCarts.Add(cart);
        var wallet = await RedeemPreviewServiceTests.SeedWalletAsync(
            dbContext,
            cart.CustomerId,
            2101,
            true,
            true,
            100m);
        RedeemPreviewServiceTests.SeedBuckets(
            dbContext,
            wallet.CustomerWalletId,
            RedeemPreviewServiceTests.CreateBucket(
                210101,
                wallet.CustomerWalletId,
                100m,
                null,
                DateTime.UtcNow));
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();
        var handler = scope.ServiceProvider.GetRequiredService<GetOrderCheckoutPreviewQueryHandler>();

        var result = await handler.Handle(
            CreateQuery(cart.CartNumber, 2101, 50m),
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(200m, result.GrossAmount);
        Assert.Equal(10m, result.DiscountAmount);
        Assert.Equal(34.20m, result.GSTAmount);
        Assert.Equal(224.20m, result.NetAmount);
        Assert.Equal(50m, result.RedemptionAmount);
        Assert.Equal(174.20m, result.FinalPayableAmount);
        Assert.NotNull(result.WalletRedemption);
        Assert.Equal(50m, result.WalletRedemption.BalanceAfter);
        Assert.Empty(dbContext.ChangeTracker.Entries());
        Assert.Equal(
            100m,
            await dbContext.CustomerWallets.AsNoTracking().Select(item => item.CurrentBalance).SingleAsync());
        Assert.Equal(
            "Active",
            await dbContext.CustomerCarts.AsNoTracking().Select(item => item.CartStatus).SingleAsync());
    }

    [Fact]
    public async Task Handle_WhenCartIsEmpty_IsRejected()
    {
        await using var scope = CreateScope();
        var cart = CreateCart("CART-PREVIEW-EMPTY", withItem: false);
        await SeedCartAsync(scope, cart);
        var handler = scope.ServiceProvider.GetRequiredService<GetOrderCheckoutPreviewQueryHandler>();

        var exception = await Assert.ThrowsAsync<DomainException>(() => handler.Handle(
            CreateQuery(cart.CartNumber),
            CancellationToken.None));

        Assert.Equal("The cart must contain at least one item before checkout.", exception.Message);
    }

    [Theory]
    [InlineData("Paid")]
    [InlineData("Cancelled")]
    public async Task Handle_WhenCartIsInactive_IsRejected(string status)
    {
        await using var scope = CreateScope();
        var cart = CreateCart($"CART-PREVIEW-{status}", withItem: true);
        if (status == "Paid")
        {
            cart.ChangeStatus("PaymentPending");
        }

        cart.ChangeStatus(status);
        await SeedCartAsync(scope, cart);
        var handler = scope.ServiceProvider.GetRequiredService<GetOrderCheckoutPreviewQueryHandler>();

        var exception = await Assert.ThrowsAsync<DomainException>(() => handler.Handle(
            CreateQuery(cart.CartNumber),
            CancellationToken.None));

        Assert.Equal("Only an active cart can be checked out.", exception.Message);
    }

    [Fact]
    public async Task Handle_WhenCartIsMissing_ReturnsNull()
    {
        await using var scope = CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<GetOrderCheckoutPreviewQueryHandler>();

        var result = await handler.Handle(
            CreateQuery("CART-PREVIEW-MISSING"),
            CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task Handle_WhenRedemptionExceedsCartNet_IsRejectedBeforeWalletPreview()
    {
        await using var scope = CreateScope();
        var cart = CreateCart("CART-PREVIEW-INVALID-REDEMPTION", withItem: true);
        await SeedCartAsync(scope, cart);
        var handler = scope.ServiceProvider.GetRequiredService<GetOrderCheckoutPreviewQueryHandler>();

        var exception = await Assert.ThrowsAsync<DomainException>(() => handler.Handle(
            CreateQuery(cart.CartNumber, 2201, 225m),
            CancellationToken.None));

        Assert.Equal("Redemption amount cannot exceed the cart net amount.", exception.Message);
    }

    [Fact]
    public async Task Handle_WhenWalletBalanceIsInsufficient_IsRejected()
    {
        await using var scope = CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var cart = CreateCart("CART-PREVIEW-INSUFFICIENT", withItem: true);
        dbContext.CustomerCarts.Add(cart);
        await RedeemPreviewServiceTests.SeedWalletAsync(
            dbContext,
            cart.CustomerId,
            2301,
            true,
            true,
            20m);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();
        var handler = scope.ServiceProvider.GetRequiredService<GetOrderCheckoutPreviewQueryHandler>();

        var exception = await Assert.ThrowsAsync<DomainException>(() => handler.Handle(
            CreateQuery(cart.CartNumber, 2301, 30m),
            CancellationToken.None));

        Assert.Equal("Insufficient wallet balance.", exception.Message);
        Assert.Empty(dbContext.ChangeTracker.Entries());
    }

    [Fact]
    public async Task Handle_WhenNoRedemptionIsRequested_DoesNotRequireWalletAndReturnsNetPayable()
    {
        await using var scope = CreateScope();
        var cart = CreateCart("CART-PREVIEW-NO-REDEMPTION", withItem: true);
        await SeedCartAsync(scope, cart);
        var handler = scope.ServiceProvider.GetRequiredService<GetOrderCheckoutPreviewQueryHandler>();

        var result = await handler.Handle(
            CreateQuery(cart.CartNumber),
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(0m, result.RedemptionAmount);
        Assert.Equal(result.NetAmount, result.FinalPayableAmount);
        Assert.Null(result.WalletRedemption);
    }

    internal static CustomerCart CreateCart(string cartNumber, bool withItem)
    {
        var cart = CustomerCart.Create(501, 7, 11, cartNumber, 41);
        if (withItem)
        {
            cart.AddItem(101, "Persisted product", 2m, 100m, 110m, 10m, 18m, 41);
        }

        return cart;
    }

    private static GetOrderCheckoutPreviewQuery CreateQuery(
        string cartNumber,
        int? walletTypeId = null,
        decimal? redemptionAmount = null) =>
        new(cartNumber, 7, 11, walletTypeId, redemptionAmount);

    private static async Task SeedCartAsync(AsyncServiceScope scope, CustomerCart cart)
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        dbContext.CustomerCarts.Add(cart);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();
    }

    private static AsyncServiceScope CreateScope()
    {
        var services = new ServiceCollection();
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        RegisterInternalRepository<ICustomerCartRepository>(services, "CustomerCartRepository");
        RegisterInternalRepository<ICustomerWalletRepository>(services, "CustomerWalletRepository");
        RegisterInternalRepository<IWalletBalanceBucketRepository>(services, "WalletBalanceBucketRepository");
        services.AddScoped<IWalletRedemptionPreviewService, WalletRedemptionPreviewService>();
        services.AddScoped<IOrderCheckoutCalculator, OrderCheckoutCalculator>();
        services.AddScoped<GetOrderCheckoutPreviewQueryHandler>();
        return services.BuildServiceProvider().CreateAsyncScope();
    }

    private static void RegisterInternalRepository<TService>(
        IServiceCollection services,
        string typeName)
        where TService : class
    {
        var implementationType = typeof(ApplicationDbContext).Assembly.GetType(
            $"Mart.Customer.Persistence.Repositories.{typeName}",
            throwOnError: true)!;
        services.AddScoped(typeof(TService), implementationType);
    }
}

public sealed class CheckoutPreviewEndpointTests : IClassFixture<CheckoutPreviewApiFactory>
{
    private readonly CheckoutPreviewApiFactory _factory;

    public CheckoutPreviewEndpointTests(CheckoutPreviewApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CheckoutPreview_WhenCartIsValid_ReturnsPaymentScreenDtoFromVersionedRoute()
    {
        var cart = CheckoutPreviewServiceTests.CreateCart("CART-PREVIEW-ENDPOINT", withItem: true);
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            dbContext.CustomerCarts.Add(cart);
            await dbContext.SaveChangesAsync();
        }

        using var client = _factory.CreateClient();
        using var response = await client.PostAsJsonAsync(
            "/api/v1/orders/checkout-preview",
            new { cartNumber = cart.CartNumber });

        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            await response.Content.ReadAsStringAsync());
        var preview = await response.Content.ReadFromJsonAsync<OrderCheckoutPreviewDto>();
        Assert.NotNull(preview);
        Assert.Equal(224.20m, preview.FinalPayableAmount);
        Assert.Null(preview.WalletRedemption);
        Assert.Single(preview.Items);
    }

    [Fact]
    public async Task CheckoutPreview_WhenCartIsMissing_ReturnsNotFound()
    {
        using var client = _factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/v1/orders/checkout-preview",
            new { cartNumber = "CART-PREVIEW-ENDPOINT-MISSING" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}

public sealed class CheckoutPreviewApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = Guid.NewGuid().ToString();

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
                options.UseInMemoryDatabase(_databaseName));

            services.RemoveAll<IInternalUserRepository>();
            services.AddScoped<IInternalUserRepository, CheckoutInternalUserRepository>();

            services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = CheckoutAuthenticationHandler.AuthenticationScheme;
                    options.DefaultChallengeScheme = CheckoutAuthenticationHandler.AuthenticationScheme;
                })
                .AddScheme<AuthenticationSchemeOptions, CheckoutAuthenticationHandler>(
                    CheckoutAuthenticationHandler.AuthenticationScheme,
                    _ => { });
        });
    }
}

internal sealed class CheckoutInternalUserRepository : IInternalUserRepository
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

internal sealed class CheckoutAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string AuthenticationScheme = "CheckoutTest";

    public CheckoutAuthenticationHandler(
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
