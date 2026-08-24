using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Mart.Customer.Application.Abstractions.Data;
using Mart.Customer.Application.Auth.Dtos;
using Mart.Customer.Application.Orders.Dtos;
using Mart.Customer.Domain.Orders;
using Mart.Customer.Persistence;
using Mart.Customer.Shared.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Mart.Customer.Tests.Orders;

public sealed class GetOrderDetailEndpointTests : IClassFixture<OrderDetailApiFactory>
{
    private readonly OrderDetailApiFactory _factory;

    public GetOrderDetailEndpointTests(OrderDetailApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetOrder_WhenOrderIsValid_ReturnsProjectedHeaderItemsPaymentsAndAvailability()
    {
        var order = await SeedOrderAsync(customerId: 7001, invoiceArchived: true);
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync($"/api/v1/orders/{order.CustomerOrderId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<OrderDetailDto>();
        Assert.NotNull(result);
        Assert.Equal(order.CustomerOrderId, result.CustomerOrderId);
        Assert.StartsWith("INV-DETAIL-", result.InvoiceNumber);
        Assert.True(result.IsInvoiceDocumentAvailable);
        Assert.Single(result.Items);
        Assert.Equal("Snapshot product", result.Items[0].ProductName);
        Assert.Single(result.Payments);
        Assert.Equal("Cash", result.Payments[0].PaymentMode);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.False(json.RootElement.TryGetProperty("invoiceArchivePath", out _));
        Assert.False(json.RootElement.TryGetProperty("storagePath", out _));
    }

    [Fact]
    public async Task GetOrder_WhenOrderDoesNotExist_ReturnsNotFound()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync("/api/v1/orders/9223372036854775000");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetOrder_WhenMobileCustomerDoesNotOwnOrder_ReturnsForbidden()
    {
        var order = await SeedOrderAsync(customerId: 7002, invoiceArchived: true);
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(OrderDetailAuthenticationHandler.CustomerIdHeader, "7999");

        using var response = await client.GetAsync($"/api/v1/orders/{order.CustomerOrderId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetInvoice_WhenMobileCustomerDoesNotOwnOrder_ReturnsForbiddenWithoutDocumentGeneration()
    {
        var order = await SeedOrderAsync(customerId: 7006, invoiceArchived: false);
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(
            OrderDetailAuthenticationHandler.CustomerIdHeader,
            (order.CustomerId + 1).ToString());

        using var response = await client.GetAsync($"/api/v1/orders/{order.CustomerOrderId}/invoice");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Empty(await dbContext.CustomerOrderInvoiceDocuments.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task GetOrder_WhenInvoiceDocumentDoesNotExist_ReturnsAvailabilityFalse()
    {
        var order = await SeedOrderAsync(customerId: 7003, invoiceArchived: false);
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(
            OrderDetailAuthenticationHandler.CustomerIdHeader,
            order.CustomerId.ToString());

        using var response = await client.GetAsync($"/api/v1/orders/{order.CustomerOrderId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<OrderDetailDto>();
        Assert.NotNull(result);
        Assert.Equal("Pending", result.InvoiceStatus);
        Assert.False(result.IsInvoiceDocumentAvailable);
    }

    [Fact]
    public async Task GetOrderByInvoice_WhenExactInvoiceExists_ReturnsThatOrder()
    {
        var order = await SeedOrderAsync(
            customerId: 7101,
            invoiceArchived: true,
            invoiceNumber: "INV-EXACT-7101");
        await SeedOrderAsync(
            customerId: 7102,
            invoiceArchived: true,
            invoiceNumber: "INV-EXACT-71010");
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync("/api/v1/orders/by-invoice/INV-EXACT-7101");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<OrderDetailDto>();
        Assert.NotNull(result);
        Assert.Equal(order.CustomerOrderId, result.CustomerOrderId);
        Assert.Equal("INV-EXACT-7101", result.InvoiceNumber);
        Assert.Single(result.Items);
        Assert.Single(result.Payments);
    }

    [Fact]
    public async Task GetOrderByInvoice_WhenInvoiceDoesNotExist_ReturnsNotFound()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync(
            "/api/v1/orders/by-invoice/INV-DOES-NOT-EXIST");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetOrderByInvoice_WhenCustomerDoesNotOwnOrder_ReturnsForbidden()
    {
        var order = await SeedOrderAsync(
            customerId: 7103,
            invoiceArchived: true,
            invoiceNumber: "INV-CUSTOMER-7103");
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(
            OrderDetailAuthenticationHandler.CustomerIdHeader,
            (order.CustomerId + 1).ToString());

        using var response = await client.GetAsync(
            $"/api/v1/orders/by-invoice/{order.InvoiceNumber}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetOrderByInvoice_WhenUnauthenticated_ReturnsUnauthorized()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(
            OrderDetailAuthenticationHandler.UnauthenticatedHeader,
            "true");

        using var response = await client.GetAsync(
            "/api/v1/orders/by-invoice/INV-PRIVATE-7104");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetOrderByInvoice_TrimsInvoiceBeforeExactLookup()
    {
        var order = await SeedOrderAsync(
            customerId: 7105,
            invoiceArchived: true,
            invoiceNumber: "INV-TRIM-7105");
        using var client = _factory.CreateClient();
        var invoiceWithWhitespace = Uri.EscapeDataString($"  {order.InvoiceNumber}  ");

        using var response = await client.GetAsync(
            $"/api/v1/orders/by-invoice/{invoiceWithWhitespace}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<OrderDetailDto>();
        Assert.NotNull(result);
        Assert.Equal(order.CustomerOrderId, result.CustomerOrderId);
    }

    [Fact]
    public async Task GetOrderByInvoice_WhenInvoiceIsTooLong_ReturnsValidationProblem()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync(
            $"/api/v1/orders/by-invoice/{new string('I', 51)}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<Microsoft.AspNetCore.Mvc.ValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal("Validation failed", problem.Title);
    }

    private async Task<CustomerOrder> SeedOrderAsync(
        long customerId,
        bool invoiceArchived,
        string? invoiceNumber = null)
    {
        var uniqueId = Random.Shared.NextInt64(1_000_000, 9_000_000_000);
        var order = CustomerOrder.Create(
            uniqueId,
            invoiceNumber ?? $"INV-DETAIL-{uniqueId}",
            customerId,
            OrderDetailInternalUserRepository.FranchiseId,
            OrderDetailInternalUserRepository.StoreId,
            new DateTime(2026, 8, 23, 10, 30, 0, DateTimeKind.Utc),
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
            "Snapshot product",
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
        await dbContext.SaveChangesAsync();
        if (invoiceArchived)
        {
            dbContext.CustomerOrderInvoiceDocuments.Add(CustomerOrderInvoiceDocument.Create(
                order.CustomerOrderId,
                order.InvoiceTemplateVersion,
                $"private/invoices/{uniqueId}.pdf",
                new string('A', 64),
                1,
                DateTime.UtcNow));
            await dbContext.SaveChangesAsync();
        }
        return order;
    }
}

public sealed class OrderDetailApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"order-detail-{Guid.NewGuid():N}";

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
            services.AddScoped<IInternalUserRepository, OrderDetailInternalUserRepository>();

            services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = OrderDetailAuthenticationHandler.AuthenticationScheme;
                    options.DefaultChallengeScheme = OrderDetailAuthenticationHandler.AuthenticationScheme;
                    options.DefaultForbidScheme = OrderDetailAuthenticationHandler.AuthenticationScheme;
                })
                .AddScheme<AuthenticationSchemeOptions, OrderDetailAuthenticationHandler>(
                    OrderDetailAuthenticationHandler.AuthenticationScheme,
                    _ => { });
        });
    }
}

internal sealed class OrderDetailInternalUserRepository : IInternalUserRepository
{
    public const long FranchiseId = 7;
    public const long StoreId = 11;

    public Task<InternalUserAccountDto?> GetByLoginIdAsync(
        string loginId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<InternalUserAccountDto?>(null);

    public Task<MartUserAccessScopeDto?> GetAccessScopeAsync(
        long userId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<MartUserAccessScopeDto?>(new MartUserAccessScopeDto(FranchiseId, StoreId));
}

internal sealed class OrderDetailAuthenticationHandler
    : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string AuthenticationScheme = "OrderDetailTest";
    public const string CustomerIdHeader = "X-Test-Customer-Id";
    public const string UnauthenticatedHeader = "X-Test-Unauthenticated";

    public OrderDetailAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (Request.Headers.ContainsKey(UnauthenticatedHeader))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var isCustomer = Request.Headers.TryGetValue(CustomerIdHeader, out var customerId);
        var userId = isCustomer ? customerId.ToString() : "41";
        var loginType = isCustomer ? "customer" : "internalUser";
        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim(MartTokenClaims.LoginType, loginType),
                new Claim(ClaimTypes.Role, isCustomer ? "Customer" : "CASHIER")
            ],
            AuthenticationScheme);
        var ticket = new AuthenticationTicket(
            new ClaimsPrincipal(identity),
            AuthenticationScheme);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
