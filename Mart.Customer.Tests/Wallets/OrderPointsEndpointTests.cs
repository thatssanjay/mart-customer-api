using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Mart.Customer.Api.Auth;
using Mart.Customer.Application.Auth.Dtos;
using Mart.Customer.Application.Auth.Queries.GetMartUserAccessScope;
using Mart.Customer.Application.Orders.Dtos;
using Mart.Customer.Domain.Carts;
using Mart.Customer.Domain.Orders;
using Mart.Customer.Persistence;
using Mart.Customer.Shared.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MediatR;
using Xunit;

namespace Mart.Customer.Tests.Wallets;

public sealed class OrderPointsEndpointTests
{
    [Fact]
    public async Task PendingPointsReturnsAllUnawardedOrdersToMartAdminNewestFirst()
    {
        await using var factory = new OrderPointsApiFactory();
        var older = CreateOrder(9101, new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc));
        var newer = CreateOrder(9101, new DateTime(2026, 9, 3, 10, 0, 0, DateTimeKind.Utc));
        var awarded = CreateOrder(9101, new DateTime(2026, 9, 4, 10, 0, 0, DateTimeKind.Utc));
        awarded.MarkPointsAwarded(new DateTime(2026, 9, 5, 10, 0, 0), "System");
        var anotherCustomersOrder = CreateOrder(9102, new DateTime(2026, 9, 6, 10, 0, 0, DateTimeKind.Utc));
        await factory.SeedAsync(older, newer, awarded, anotherCustomersOrder);
        using var client = factory.CreateMartAdminJwtClient();

        using var response = await client.GetAsync("/api/v1/wallet-engine/orders/pending-points");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var orders = await response.Content.ReadFromJsonAsync<List<PendingPointsOrderDto>>();
        Assert.NotNull(orders);
        Assert.Equal(
            [anotherCustomersOrder.CustomerOrderId, newer.CustomerOrderId, older.CustomerOrderId],
            orders.Select(order => order.CustomerOrderId));
        Assert.All(orders, order => Assert.True(order.FinalPayableAmount > 0));
    }

    [Fact]
    public async Task MarkPointsAwardedUpdatesOrderInMartAdminsStoreOnce()
    {
        await using var factory = new OrderPointsApiFactory();
        var order = CreateOrder(9201, new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc));
        await factory.SeedAsync(order);
        using var client = factory.CreateInternalUserClient();

        using var firstResponse = await client.PutAsync(
            $"/api/v1/wallet-engine/orders/{order.CustomerOrderId}/points-awarded",
            null);

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        var result = await firstResponse.Content.ReadFromJsonAsync<OrderPointsAwardedDto>();
        Assert.NotNull(result);
        Assert.Equal(order.CustomerOrderId, result.OrderId);
        Assert.True(result.IsPointsAwarded);
        Assert.NotNull(result.PointsAwardedDate);
        Assert.Equal("System", result.PointsAwardedBy);

        using var secondResponse = await client.PutAsync(
            $"/api/v1/wallet-engine/orders/{order.CustomerOrderId}/points-awarded",
            null);
        Assert.Equal(HttpStatusCode.BadRequest, secondResponse.StatusCode);
        var problem = await secondResponse.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains("already been awarded", problem.Errors["orderId"].Single());
    }

    [Fact]
    public async Task MarkPointsAwardedRejectsOrderOutsideMartAdminsStoreWithoutUpdatingIt()
    {
        await using var factory = new OrderPointsApiFactory();
        var order = CreateOrder(
            9302,
            new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc),
            storeId: 12);
        await factory.SeedAsync(order);
        using var client = factory.CreateInternalUserClient();

        using var response = await client.PutAsync(
            $"/api/v1/wallet-engine/orders/{order.CustomerOrderId}/points-awarded",
            null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var persisted = await factory.GetOrderAsync(order.CustomerOrderId);
        Assert.NotNull(persisted);
        Assert.False(persisted.IsPointsAwarded);
        Assert.Null(persisted.PointsAwardedDate);
        Assert.Null(persisted.PointsAwardedBy);
    }

    [Theory]
    [InlineData("1/points-awarded", "PUT")]
    public async Task MarkPointsAwardedEndpointRejectsCustomerTokens(string path, string method)
    {
        await using var factory = new OrderPointsApiFactory();
        using var client = factory.CreateCustomerClient(9201);
        using var request = new HttpRequestMessage(new HttpMethod(method), $"/api/v1/wallet-engine/orders/{path}");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("pending-points", "GET")]
    [InlineData("1/points-awarded", "PUT")]
    [InlineData("1/credit", "POST")]
    public async Task WalletEngineEndpointsAllowMartAdminTokens(string path, string method)
    {
        await using var factory = new OrderPointsApiFactory();
        await factory.SeedAsync();
        using var client = path == "pending-points"
            ? factory.CreateMartAdminJwtClient()
            : factory.CreateInternalUserClient();
        using var request = new HttpRequestMessage(
            new HttpMethod(method),
            $"/api/v1/wallet-engine/orders/{path}");

        using var response = await client.SendAsync(request);

        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("pending-points", "GET", "internalUser", "FRANCHISE_ADMIN")]
    [InlineData("1/points-awarded", "PUT", "internalUser", "FRANCHISE_ADMIN")]
    [InlineData("1/credit", "POST", "internalUser", "FRANCHISE_ADMIN")]
    [InlineData("pending-points", "GET", "internalUser", "CASHIER")]
    [InlineData("1/points-awarded", "PUT", "internalUser", "CASHIER")]
    [InlineData("1/credit", "POST", "internalUser", "CASHIER")]
    [InlineData("pending-points", "GET", "internalUser", "STAFF")]
    [InlineData("1/points-awarded", "PUT", "internalUser", "STAFF")]
    [InlineData("1/credit", "POST", "internalUser", "STAFF")]
    [InlineData("pending-points", "GET", "customer", "CUSTOMER")]
    [InlineData("1/points-awarded", "PUT", "customer", "CUSTOMER")]
    [InlineData("1/credit", "POST", "customer", "CUSTOMER")]
    public async Task WalletEngineEndpointsRejectNonMartAdminRoles(
        string path,
        string method,
        string loginType,
        string role)
    {
        await using var factory = new OrderPointsApiFactory();
        using var client = path == "pending-points"
            ? factory.CreateMartAdminJwtClient(role)
            : factory.CreateAuthenticatedClient(loginType, role);
        using var request = new HttpRequestMessage(
            new HttpMethod(method),
            $"/api/v1/wallet-engine/orders/{path}");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("pending-points", "GET")]
    [InlineData("1/points-awarded", "PUT")]
    [InlineData("1/credit", "POST")]
    public async Task WalletEngineEndpointsRejectMissingTokens(string path, string method)
    {
        await using var factory = new OrderPointsApiFactory();
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(
            new HttpMethod(method),
            $"/api/v1/wallet-engine/orders/{path}");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("pending-points", "GET")]
    [InlineData("1/points-awarded", "PUT")]
    [InlineData("1/credit", "POST")]
    public async Task WalletEngineEndpointsRejectInvalidTokens(string path, string method)
    {
        await using var factory = new OrderPointsApiFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(OrderPointsAuthenticationHandler.InvalidTokenHeader, "true");
        using var request = new HttpRequestMessage(
            new HttpMethod(method),
            $"/api/v1/wallet-engine/orders/{path}");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static CustomerOrder CreateOrder(long customerId, DateTime orderDate, long storeId = 11)
    {
        var uniqueId = Random.Shared.NextInt64(10_000_000_000, 90_000_000_000);
        return CustomerOrder.Create(
            uniqueId,
            $"INV-POINTS-{uniqueId}",
            customerId,
            7,
            storeId,
            orderDate,
            0,
            118m,
            0m,
            18m,
            118m,
            null,
            0m,
            118m,
            41);
    }
}

internal sealed class OrderPointsApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"MartOrderPointsTests_{Guid.NewGuid():N}";
    private bool _databaseDeleted;
    private string ConnectionString =>
        $"Server=(localdb)\\MSSQLLocalDB;Database={_databaseName};Integrated Security=true;TrustServerCertificate=true";

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
            services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(ConnectionString));
            services.RemoveAll<IRequestHandler<GetMartUserAccessScopeQuery, MartUserAccessScopeDto>>();
            services.AddSingleton<
                IRequestHandler<GetMartUserAccessScopeQuery, MartUserAccessScopeDto>,
                OrderPointsTestAccess>();

            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = OrderPointsAuthenticationHandler.AuthenticationScheme;
                options.DefaultChallengeScheme = OrderPointsAuthenticationHandler.AuthenticationScheme;
                options.DefaultForbidScheme = OrderPointsAuthenticationHandler.AuthenticationScheme;
            }).AddScheme<AuthenticationSchemeOptions, OrderPointsAuthenticationHandler>(
                OrderPointsAuthenticationHandler.AuthenticationScheme,
                _ => { });
        });
    }

    public HttpClient CreateCustomerClient(long customerId)
        => CreateAuthenticatedClient("customer", "CUSTOMER", customerId.ToString());

    public HttpClient CreateInternalUserClient(string role = "MA")
        => CreateAuthenticatedClient("internalUser", role);

    public HttpClient CreateMartAdminJwtClient(string role = MartAuthorizationPolicies.MartAdminRole)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add(OrderPointsAuthenticationHandler.UserIdHeader, "41");
        client.DefaultRequestHeaders.Add(OrderPointsAuthenticationHandler.RoleHeader, role);
        client.DefaultRequestHeaders.Add(OrderPointsAuthenticationHandler.MartJwtHeader, "true");
        return client;
    }

    public HttpClient CreateAuthenticatedClient(string loginType, string role, string userId = "41")
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add(OrderPointsAuthenticationHandler.UserIdHeader, userId);
        client.DefaultRequestHeaders.Add(OrderPointsAuthenticationHandler.LoginTypeHeader, loginType);
        client.DefaultRequestHeaders.Add(OrderPointsAuthenticationHandler.RoleHeader, role);
        return client;
    }

    public async Task SeedAsync(params CustomerOrder[] orders)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await dbContext.Database.EnsureCreatedAsync();
        var carts = orders.Select(order => CustomerCart.Create(
            order.CustomerId,
            order.FranchiseId,
            order.MartStoreId,
            $"CART-POINTS-{Guid.NewGuid():N}",
            order.CreatedBy)).ToList();
        await dbContext.CustomerCarts.AddRangeAsync(carts);
        await dbContext.SaveChangesAsync();
        for (var index = 0; index < orders.Length; index++)
        {
            typeof(CustomerOrder)
                .GetProperty(nameof(CustomerOrder.CustomerCartId), BindingFlags.Instance | BindingFlags.Public)!
                .SetValue(orders[index], carts[index].CustomerCartId);
        }
        await dbContext.CustomerOrders.AddRangeAsync(orders);
        await dbContext.SaveChangesAsync();
    }

    public async Task<CustomerOrder?> GetOrderAsync(long orderId)
    {
        await using var scope = Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>()
            .CustomerOrders
            .AsNoTracking()
            .SingleOrDefaultAsync(order => order.CustomerOrderId == orderId);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_databaseDeleted)
        {
            _databaseDeleted = true;
            using var dbContext = new ApplicationDbContext(
                new DbContextOptionsBuilder<ApplicationDbContext>()
                    .UseSqlServer(ConnectionString)
                    .Options);
            dbContext.Database.EnsureDeleted();
        }

        base.Dispose(disposing);
    }
}

internal sealed class OrderPointsTestAccess
    : IRequestHandler<GetMartUserAccessScopeQuery, MartUserAccessScopeDto>
{
    public Task<MartUserAccessScopeDto> Handle(
        GetMartUserAccessScopeQuery request,
        CancellationToken cancellationToken) =>
        Task.FromResult(new MartUserAccessScopeDto(7, 11));
}

internal sealed class OrderPointsAuthenticationHandler
    : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string AuthenticationScheme = "OrderPointsTest";
    public const string UserIdHeader = "X-Test-User-Id";
    public const string LoginTypeHeader = "X-Test-Login-Type";
    public const string RoleHeader = "X-Test-Role";
    public const string InvalidTokenHeader = "X-Test-Invalid-Token";
    public const string MartJwtHeader = "X-Test-Mart-Jwt";

    public OrderPointsAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (Request.Headers.ContainsKey(InvalidTokenHeader))
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid test token."));
        }

        var userId = Request.Headers[UserIdHeader].SingleOrDefault();
        var loginType = Request.Headers[LoginTypeHeader].SingleOrDefault();
        var role = Request.Headers[RoleHeader].SingleOrDefault();
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(role))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        if (Request.Headers.ContainsKey(MartJwtHeader))
        {
            var martIdentity = new ClaimsIdentity(
            [
                new Claim(MartTokenClaims.UserId, userId),
                new Claim(MartTokenClaims.LegacyUserRole, role)
            ], AuthenticationScheme);
            var martTicket = new AuthenticationTicket(
                new ClaimsPrincipal(martIdentity),
                AuthenticationScheme);
            return Task.FromResult(AuthenticateResult.Success(martTicket));
        }

        if (string.IsNullOrWhiteSpace(loginType))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var identity = new ClaimsIdentity(
        [
            new Claim(MartTokenClaims.UserId, userId),
            new Claim(MartTokenClaims.LoginType, loginType),
            new Claim(ClaimTypes.Role, role)
        ], AuthenticationScheme);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), AuthenticationScheme);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
