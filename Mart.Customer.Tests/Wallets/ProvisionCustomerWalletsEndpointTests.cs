using System.Net;
using System.Net.Http.Json;
using Mart.Customer.Application.Wallets.Dtos;
using Mart.Customer.Domain.Wallets;
using Mart.Customer.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using CustomerEntity = Mart.Customer.Domain.Customers.Customer;

namespace Mart.Customer.Tests.Wallets;

public sealed class ProvisionCustomerWalletsEndpointTests
    : IClassFixture<ProvisionCustomerWalletsApiFactory>
{
    private readonly ProvisionCustomerWalletsApiFactory _factory;

    public ProvisionCustomerWalletsEndpointTests(ProvisionCustomerWalletsApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ProvisionWallets_WhenRequestIsValid_ReturnsCreatedAndSkippedWallets()
    {
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            dbContext.Customers.Add(CreateCustomer(20011));
            dbContext.WalletTypes.AddRange(
                CreateWalletType(1, "Reward", "REWARD"),
                CreateWalletType(2, "Wallet", "WALLET"));
            dbContext.CustomerWallets.Add(CustomerWallet.Create(20011, 1, DateTime.UtcNow));
            await dbContext.SaveChangesAsync();
        }

        using var client = _factory.CreateClient();
        using var response = await client.PostAsync(
            "/api/customers/20011/wallets/provision",
            content: null,
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ProvisionCustomerWalletsResultDto>();
        Assert.NotNull(result);
        Assert.Equal(20011, result.CustomerId);
        Assert.Collection(result.Created, wallet => Assert.Equal(2, wallet.WalletTypeId));
        Assert.Collection(result.Skipped, wallet => Assert.Equal(1, wallet.WalletTypeId));
    }

    [Fact]
    public async Task ProvisionWallets_WhenCustomerIdIsInvalid_ReturnsStandardValidationProblem()
    {
        using var client = _factory.CreateClient();

        using var response = await client.PostAsync(
            "/api/customers/0/wallets/provision",
            content: null,
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.Status);
        Assert.Equal("Validation failed", problem.Title);
    }

    private static CustomerEntity CreateCustomer(long customerId)
    {
        var customer = CustomerEntity.Create(
            $"CUS-{customerId}",
            null,
            null,
            $"Customer {customerId}",
            customerId.ToString(),
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            false,
            false,
            true,
            false,
            null,
            DateTime.UtcNow,
            null);
        SetProperty(customer, nameof(CustomerEntity.CustomerId), customerId);
        return customer;
    }

    private static WalletType CreateWalletType(int id, string name, string code)
    {
        var walletType = (WalletType)Activator.CreateInstance(typeof(WalletType), nonPublic: true)!;
        SetProperty(walletType, nameof(WalletType.Id), id);
        SetProperty(walletType, nameof(WalletType.Name), name);
        SetProperty(walletType, nameof(WalletType.Code), code);
        SetProperty(walletType, nameof(WalletType.IsActive), true);
        SetProperty(walletType, nameof(WalletType.CreatedDate), DateTime.UtcNow);
        return walletType;
    }

    private static void SetProperty<TTarget, TValue>(TTarget target, string propertyName, TValue value)
        where TTarget : class
    {
        typeof(TTarget).GetProperty(propertyName)!.SetValue(target, value);
    }
}

public sealed class ProvisionCustomerWalletsApiFactory : WebApplicationFactory<Program>
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
                .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, TestAuthenticationHandler>(
                    TestAuthenticationHandler.AuthenticationScheme,
                    _ => { });
        });
    }
}
