using System.Net;
using System.Net.Http.Json;
using FluentValidation.TestHelper;
using Mart.Customer.Application.Abstractions.Data;
using Mart.Customer.Application.Wallets.Commands.UpdateCustomerWalletStatus;
using Mart.Customer.Application.Wallets.Dtos;
using Mart.Customer.Domain.Common;
using Mart.Customer.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Mart.Customer.Tests.Wallets;

public sealed class UpdateCustomerWalletStatusServiceTests
{
    [Fact]
    public async Task Handle_UpdatesOnlyStatusAndModifiedOn()
    {
        await using var scope = CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await CreditWalletServiceTests.SeedWalletAsync(
            dbContext,
            12001,
            1201,
            walletIsActive: true,
            walletTypeIsActive: true,
            balance: 25m,
            totalCredit: 40m);
        var handler = scope.ServiceProvider.GetRequiredService<UpdateCustomerWalletStatusCommandHandler>();
        var startedOn = DateTime.UtcNow;

        var result = await handler.Handle(
            new UpdateCustomerWalletStatusCommand(12001, 1201, false),
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.False(result.IsActive);
        Assert.NotNull(result.ModifiedOn);
        Assert.InRange(result.ModifiedOn.Value, startedOn, DateTime.UtcNow);

        var wallet = await dbContext.CustomerWallets.AsNoTracking().SingleAsync();
        Assert.False(wallet.IsActive);
        Assert.Equal(25m, wallet.CurrentBalance);
        Assert.Equal(40m, wallet.TotalCredit);
        Assert.Equal(0m, wallet.TotalDebit);
        Assert.Equal(0m, wallet.TotalExpired);
        Assert.Empty(await dbContext.WalletTransactions.AsNoTracking().ToListAsync());
        Assert.Empty(await dbContext.WalletBalanceBuckets.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task Handle_WhenStatusIsUnchanged_IsIdempotentAndPreservesModifiedOn()
    {
        await using var scope = CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await CreditWalletServiceTests.SeedWalletAsync(
            dbContext,
            12002,
            1202,
            walletIsActive: true,
            walletTypeIsActive: true,
            balance: 0m,
            totalCredit: 0m);
        var handler = scope.ServiceProvider.GetRequiredService<UpdateCustomerWalletStatusCommandHandler>();

        var result = await handler.Handle(
            new UpdateCustomerWalletStatusCommand(12002, 1202, true),
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(result.IsActive);
        Assert.Null(result.ModifiedOn);
        Assert.Null((await dbContext.CustomerWallets.AsNoTracking().SingleAsync()).ModifiedOn);
    }

    [Fact]
    public async Task Handle_ActivatesInactiveWallet()
    {
        await using var scope = CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await CreditWalletServiceTests.SeedWalletAsync(
            dbContext,
            12004,
            1204,
            walletIsActive: false,
            walletTypeIsActive: true,
            balance: 0m,
            totalCredit: 0m);
        var handler = scope.ServiceProvider.GetRequiredService<UpdateCustomerWalletStatusCommandHandler>();

        var result = await handler.Handle(
            new UpdateCustomerWalletStatusCommand(12004, 1204, true),
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(result.IsActive);
        Assert.NotNull(result.ModifiedOn);
    }

    [Fact]
    public async Task Handle_WhenWalletDoesNotExist_ReturnsNull()
    {
        await using var scope = CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<UpdateCustomerWalletStatusCommandHandler>();

        var result = await handler.Handle(
            new UpdateCustomerWalletStatusCommand(12999, 1299, false),
            CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task Handle_WhenWalletTypeIsInactive_RejectsUpdate()
    {
        await using var scope = CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await CreditWalletServiceTests.SeedWalletAsync(
            dbContext,
            12003,
            1203,
            walletIsActive: true,
            walletTypeIsActive: false,
            balance: 0m,
            totalCredit: 0m);
        var handler = scope.ServiceProvider.GetRequiredService<UpdateCustomerWalletStatusCommandHandler>();

        var exception = await Assert.ThrowsAsync<DomainException>(() => handler.Handle(
            new UpdateCustomerWalletStatusCommand(12003, 1203, false),
            CancellationToken.None));

        Assert.Equal("Wallet type is inactive.", exception.Message);
        Assert.True((await dbContext.CustomerWallets.AsNoTracking().SingleAsync()).IsActive);
    }

    [Fact]
    public void Validator_RejectsInvalidIdsAndMissingStatus()
    {
        var validator = new UpdateCustomerWalletStatusCommandValidator();

        var result = validator.TestValidate(
            new UpdateCustomerWalletStatusCommand(0, 0, null));

        result.ShouldHaveValidationErrorFor(command => command.CustomerId);
        result.ShouldHaveValidationErrorFor(command => command.WalletTypeId);
        result.ShouldHaveValidationErrorFor(command => command.IsActive);
    }

    private static AsyncServiceScope CreateScope()
    {
        var services = new ServiceCollection();
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        RegisterInternalRepository<ICustomerWalletRepository>(services, "CustomerWalletRepository");
        RegisterInternalRepository<IWalletTypeRepository>(services, "WalletTypeRepository");
        RegisterInternalRepository<IUnitOfWork>(services, "UnitOfWork");
        services.AddScoped<UpdateCustomerWalletStatusCommandHandler>();
        return services.BuildServiceProvider().CreateAsyncScope();
    }

    private static void RegisterInternalRepository<TService>(IServiceCollection services, string typeName)
        where TService : class
    {
        var implementationType = typeof(ApplicationDbContext).Assembly.GetType(
            $"Mart.Customer.Persistence.Repositories.{typeName}",
            throwOnError: true)!;
        services.AddScoped(typeof(TService), implementationType);
    }
}

public sealed class UpdateCustomerWalletStatusEndpointTests
    : IClassFixture<GetCustomerWalletsApiFactory>
{
    private readonly GetCustomerWalletsApiFactory _factory;

    public UpdateCustomerWalletStatusEndpointTests(GetCustomerWalletsApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task UpdateStatus_WhenRequestIsValid_ReturnsUpdatedWalletStatus()
    {
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await CreditWalletServiceTests.SeedWalletAsync(
                dbContext,
                12101,
                1211,
                walletIsActive: true,
                walletTypeIsActive: true,
                balance: 15m,
                totalCredit: 15m);
        }

        using var client = _factory.CreateClient();
        using var response = await client.PatchAsJsonAsync(
            "/api/customers/12101/wallets/1211/status",
            new { IsActive = false });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<UpdatedCustomerWalletStatusDto>();
        Assert.NotNull(result);
        Assert.Equal(12101, result.CustomerId);
        Assert.Equal(1211, result.WalletTypeId);
        Assert.False(result.IsActive);
        Assert.NotNull(result.ModifiedOn);
    }

    [Fact]
    public async Task UpdateStatus_WhenStatusIsMissing_ReturnsStandardValidationProblem()
    {
        using var client = _factory.CreateClient();
        using var response = await client.PatchAsJsonAsync(
            "/api/customers/12102/wallets/1212/status",
            new { });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.Status);
        Assert.Equal("Validation failed", problem.Title);
    }

    [Fact]
    public async Task UpdateStatus_WhenWalletDoesNotExist_ReturnsNotFound()
    {
        using var client = _factory.CreateClient();
        using var response = await client.PatchAsJsonAsync(
            "/api/customers/12103/wallets/1213/status",
            new { IsActive = false });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateStatus_WhenUnauthenticated_ReturnsUnauthorized()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        using var response = await client.PatchAsJsonAsync(
            "/api/customers/12104/wallets/1214/status",
            new { IsActive = false });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
