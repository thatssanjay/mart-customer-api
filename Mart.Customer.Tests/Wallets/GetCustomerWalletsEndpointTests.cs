using System.Net;
using System.Net.Http.Json;
using Mart.Customer.Application.Wallets.Dtos;
using Mart.Customer.Domain.Wallets;
using Mart.Customer.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.Swagger;

namespace Mart.Customer.Tests.Wallets;

public sealed class GetCustomerWalletsEndpointTests
    : IClassFixture<GetCustomerWalletsApiFactory>
{
    private readonly GetCustomerWalletsApiFactory _factory;

    public GetCustomerWalletsEndpointTests(GetCustomerWalletsApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetWallets_WhenRequestIsValid_ReturnsProjectedActiveWallets()
    {
        await SeedAsync(
            301,
            CreateWalletType(11, "Cashback", "CASHBACK", true),
            CreateWalletType(12, "Archived", "ARCHIVED", false));

        using var client = _factory.CreateClient();
        using var response = await client.GetAsync(
            "/api/customers/301/wallets",
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var wallets = await response.Content.ReadFromJsonAsync<List<CustomerWalletDto>>(
            CancellationToken.None);
        var wallet = Assert.Single(Assert.IsType<List<CustomerWalletDto>>(wallets));
        Assert.Equal(301, wallet.CustomerId);
        Assert.Equal(11, wallet.WalletTypeId);
        Assert.Equal("Cashback", wallet.WalletTypeName);
    }

    [Fact]
    public async Task GetWalletDetail_WhenRequestIsValid_ReturnsProjectedActiveWallet()
    {
        await SeedAsync(
            303,
            CreateWalletType(31, "Cashback detail", "CASHBACK_DETAIL", true));

        using var client = _factory.CreateClient();
        using var response = await client.GetAsync(
            "/api/customers/303/wallets/31",
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var wallet = await response.Content.ReadFromJsonAsync<CustomerWalletDto>(
            CancellationToken.None);
        Assert.NotNull(wallet);
        Assert.Equal(303, wallet.CustomerId);
        Assert.Equal(31, wallet.WalletTypeId);
        Assert.Equal("Cashback detail", wallet.WalletTypeName);
        Assert.Equal("CASHBACK_DETAIL", wallet.WalletTypeCode);
        Assert.True(wallet.IsActive);
        Assert.True(wallet.IsWalletTypeActive);
    }

    [Fact]
    public void GetWalletDetail_OpenApiParametersMatchRouteTokens()
    {
        using var scope = _factory.Services.CreateScope();
        var swaggerProvider = scope.ServiceProvider.GetRequiredService<ISwaggerProvider>();
        var document = swaggerProvider.GetSwagger("v1");
        var operation = document.Paths["/api/customers/{customerId}/wallets/{walletTypeId}"]
            .Operations![HttpMethod.Get];

        var routeParameters = operation.Parameters!
            .Where(parameter => parameter.In == ParameterLocation.Path);

        Assert.Collection(
            routeParameters,
            parameter => Assert.Equal("customerId", parameter.Name),
            parameter => Assert.Equal("walletTypeId", parameter.Name));
    }

    [Fact]
    public async Task GetWalletDetail_WhenWalletTypeIdIsInvalid_ReturnsStandardValidationProblem()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync(
            "/api/customers/303/wallets/0",
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(
            CancellationToken.None);
        Assert.NotNull(problem);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.Status);
        Assert.Equal("Validation failed", problem.Title);
    }

    [Fact]
    public async Task GetWalletDetail_WhenWalletDoesNotBelongToCustomer_ReturnsNotFound()
    {
        await SeedAsync(
            304,
            CreateWalletType(41, "Rewards", "REWARDS", true));

        using var client = _factory.CreateClient();
        using var response = await client.GetAsync(
            "/api/customers/999/wallets/41",
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetWalletDetail_WhenWalletTypeIsInactive_ReturnsNotFound()
    {
        await SeedAsync(
            305,
            CreateWalletType(51, "Archived", "ARCHIVED_DETAIL", false));

        using var client = _factory.CreateClient();
        using var response = await client.GetAsync(
            "/api/customers/305/wallets/51",
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetWalletDetail_WhenUnauthenticated_ReturnsUnauthorized()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(
            "/api/customers/303/wallets/31",
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetWallets_WhenIncludeInactiveIsTrue_ReturnsInactiveWalletTypes()
    {
        await SeedAsync(
            302,
            CreateWalletType(21, "Active", "ACTIVE", true),
            CreateWalletType(22, "Inactive", "INACTIVE", false));

        using var client = _factory.CreateClient();
        using var response = await client.GetAsync(
            "/api/customers/302/wallets?includeInactive=true",
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var wallets = await response.Content.ReadFromJsonAsync<List<CustomerWalletDto>>(
            CancellationToken.None);
        Assert.Equal(2, Assert.IsType<List<CustomerWalletDto>>(wallets).Count);
        Assert.Contains(wallets, wallet => !wallet.IsWalletTypeActive);
    }

    [Fact]
    public async Task GetWallets_WhenCustomerIdIsInvalid_ReturnsStandardValidationProblem()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync(
            "/api/customers/0/wallets",
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(
            CancellationToken.None);
        Assert.NotNull(problem);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.Status);
        Assert.Equal("Validation failed", problem.Title);
    }

    private async Task SeedAsync(
        long customerId,
        params WalletType[] walletTypes)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        dbContext.WalletTypes.AddRange(walletTypes);
        dbContext.CustomerWallets.AddRange(
            walletTypes.Select(walletType =>
                CustomerWallet.Create(customerId, walletType.Id, DateTime.UtcNow)));
        await dbContext.SaveChangesAsync(CancellationToken.None);
    }

    private static WalletType CreateWalletType(int id, string name, string code, bool isActive)
    {
        var walletType = (WalletType)Activator.CreateInstance(typeof(WalletType), nonPublic: true)!;
        SetProperty(walletType, nameof(WalletType.Id), id);
        SetProperty(walletType, nameof(WalletType.Name), name);
        SetProperty(walletType, nameof(WalletType.Code), code);
        SetProperty(walletType, nameof(WalletType.IsActive), isActive);
        SetProperty(walletType, nameof(WalletType.CreatedDate), DateTime.UtcNow);
        return walletType;
    }

    private static void SetProperty<TTarget, TValue>(TTarget target, string propertyName, TValue value)
        where TTarget : class
    {
        typeof(TTarget).GetProperty(propertyName)!.SetValue(target, value);
    }
}

public sealed class GetCustomerWalletsApiFactory : WebApplicationFactory<Program>
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

            services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = TestAuthenticationHandler.AuthenticationScheme;
                    options.DefaultChallengeScheme = TestAuthenticationHandler.AuthenticationScheme;
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                    TestAuthenticationHandler.AuthenticationScheme,
                    _ => { });
        });
    }
}
