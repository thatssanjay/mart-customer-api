using System.Net;
using System.Net.Http.Json;
using FluentValidation.TestHelper;
using Mart.Customer.Application.Abstractions.Data;
using Mart.Customer.Application.Wallets.Commands.CreditWallet;
using Mart.Customer.Application.Wallets.Dtos;
using Mart.Customer.Domain.Common;
using Mart.Customer.Domain.Wallets;
using Mart.Customer.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Mart.Customer.Tests.Wallets;

public sealed class CreditWalletServiceTests
{
    [Fact]
    public async Task Handle_CreditsWalletAndCreatesLedgerAndLinkedBucket()
    {
        await using var scope = CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var expiryDate = DateTime.UtcNow.AddDays(30);
        await SeedWalletAsync(dbContext, 9101, 911, true, true, 10m, 10m);
        var handler = scope.ServiceProvider.GetRequiredService<CreditWalletCommandHandler>();

        var result = await handler.Handle(
            CreateCommand(9101, 911, 25m, "ORDER", 51001, expiryDate),
            CancellationToken.None);

        Assert.StartsWith("TX-", result.TransactionNumber);
        Assert.Equal(10m, result.BalanceBefore);
        Assert.Equal(35m, result.BalanceAfter);
        Assert.Equal(35m, result.TotalCredit);
        var wallet = await dbContext.CustomerWallets.SingleAsync();
        Assert.Equal(35m, wallet.CurrentBalance);
        Assert.Equal(35m, wallet.TotalCredit);
        var transaction = await dbContext.WalletTransactions.SingleAsync();
        Assert.Equal("CREDIT", transaction.TransactionType);
        Assert.Equal(25m, transaction.Amount);
        Assert.Equal("ORDER", transaction.ReferenceType);
        var bucket = await dbContext.WalletBalanceBuckets.SingleAsync();
        Assert.Equal(transaction.WalletTransactionId, bucket.SourceTransactionId);
        Assert.Equal(25m, bucket.OriginalAmount);
        Assert.Equal(25m, bucket.AvailableAmount);
        Assert.Equal(expiryDate, bucket.ExpiryDate);
    }

    [Fact]
    public async Task Handle_RepeatedReferenceReturnsOriginalCreditWithoutCreditingAgain()
    {
        await using var scope = CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await SeedWalletAsync(dbContext, 9201, 921, true, true, 0m, 0m);
        var handler = scope.ServiceProvider.GetRequiredService<CreditWalletCommandHandler>();
        var command = CreateCommand(9201, 921, 15m, "order", 52001, null);

        var first = await handler.Handle(command, CancellationToken.None);
        var repeated = await handler.Handle(command, CancellationToken.None);

        Assert.Equal(first.WalletTransactionId, repeated.WalletTransactionId);
        Assert.Equal(first.TransactionNumber, repeated.TransactionNumber);
        Assert.Equal(15m, (await dbContext.CustomerWallets.SingleAsync()).CurrentBalance);
        Assert.Equal(1, await dbContext.WalletTransactions.CountAsync());
        Assert.Equal(1, await dbContext.WalletBalanceBuckets.CountAsync());
    }

    [Fact]
    public async Task Handle_ReusedReferenceWithDifferentAmountIsRejectedWithoutChangingBalance()
    {
        await using var scope = CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await SeedWalletAsync(dbContext, 9301, 931, true, true, 0m, 0m);
        var handler = scope.ServiceProvider.GetRequiredService<CreditWalletCommandHandler>();
        await handler.Handle(
            CreateCommand(9301, 931, 10m, "ORDER", 53001, null),
            CancellationToken.None);

        var exception = await Assert.ThrowsAsync<DomainException>(() => handler.Handle(
            CreateCommand(9301, 931, 11m, "ORDER", 53001, null),
            CancellationToken.None));

        Assert.Contains("different credit details", exception.Message);
        Assert.Equal(10m, (await dbContext.CustomerWallets.SingleAsync()).CurrentBalance);
        Assert.Equal(1, await dbContext.WalletTransactions.CountAsync());
    }

    [Theory]
    [InlineData(false, true, "Customer wallet is inactive.")]
    [InlineData(true, false, "Wallet type is inactive.")]
    public async Task Handle_WhenWalletOrWalletTypeIsInactive_IsRejected(
        bool walletIsActive,
        bool walletTypeIsActive,
        string expectedMessage)
    {
        await using var scope = CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await SeedWalletAsync(
            dbContext,
            walletIsActive ? 9401 : 9402,
            walletIsActive ? 941 : 942,
            walletIsActive,
            walletTypeIsActive,
            0m,
            0m);
        var handler = scope.ServiceProvider.GetRequiredService<CreditWalletCommandHandler>();

        var exception = await Assert.ThrowsAsync<DomainException>(() => handler.Handle(
            CreateCommand(
                walletIsActive ? 9401 : 9402,
                walletIsActive ? 941 : 942,
                10m,
                null,
                null,
                null),
            CancellationToken.None));

        Assert.Equal(expectedMessage, exception.Message);
        Assert.Empty(await dbContext.WalletTransactions.ToListAsync());
        Assert.Empty(await dbContext.WalletBalanceBuckets.ToListAsync());
    }

    [Fact]
    public void Validator_RejectsInvalidIdsAmountReferencePairAndPastExpiry()
    {
        var validator = new CreditWalletCommandValidator();
        var command = new CreditWalletCommand(
            0,
            0,
            0.001m,
            "ORDER",
            null,
            null,
            DateTime.UtcNow.AddMinutes(-1),
            string.Empty);

        var result = validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(item => item.CustomerId);
        result.ShouldHaveValidationErrorFor(item => item.WalletTypeId);
        result.ShouldHaveValidationErrorFor(item => item.Amount);
        result.ShouldHaveValidationErrorFor(item => item.ReferenceId);
        result.ShouldHaveValidationErrorFor(item => item.ExpiryDate);
        result.ShouldHaveValidationErrorFor(item => item.CreatedBy);
    }

    private static AsyncServiceScope CreateScope()
    {
        var services = new ServiceCollection();
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        RegisterInternalRepository<ICustomerWalletRepository>(services, "CustomerWalletRepository");
        RegisterInternalRepository<IWalletTypeRepository>(services, "WalletTypeRepository");
        RegisterInternalRepository<IWalletTransactionRepository>(services, "WalletTransactionRepository");
        RegisterInternalRepository<IWalletBalanceBucketRepository>(services, "WalletBalanceBucketRepository");
        RegisterInternalRepository<IUnitOfWork>(services, "UnitOfWork");
        services.AddScoped<CreditWalletCommandHandler>();
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

    private static CreditWalletCommand CreateCommand(
        long customerId,
        int walletTypeId,
        decimal amount,
        string? referenceType,
        long? referenceId,
        DateTime? expiryDate)
    {
        return new CreditWalletCommand(
            customerId,
            walletTypeId,
            amount,
            referenceType,
            referenceId,
            "Credit test",
            expiryDate,
            "wallet-tests");
    }

    internal static async Task SeedWalletAsync(
        ApplicationDbContext dbContext,
        long customerId,
        int walletTypeId,
        bool walletIsActive,
        bool walletTypeIsActive,
        decimal balance,
        decimal totalCredit)
    {
        var walletType = (WalletType)Activator.CreateInstance(typeof(WalletType), nonPublic: true)!;
        SetProperty(walletType, nameof(WalletType.Id), walletTypeId);
        SetProperty(walletType, nameof(WalletType.Name), $"Wallet {walletTypeId}");
        SetProperty(walletType, nameof(WalletType.Code), $"WALLET_{walletTypeId}");
        SetProperty(walletType, nameof(WalletType.IsActive), walletTypeIsActive);
        SetProperty(walletType, nameof(WalletType.CreatedDate), DateTime.UtcNow);
        var wallet = CustomerWallet.Create(customerId, walletTypeId, DateTime.UtcNow);
        SetProperty(wallet, nameof(CustomerWallet.IsActive), walletIsActive);
        SetProperty(wallet, nameof(CustomerWallet.CurrentBalance), balance);
        SetProperty(wallet, nameof(CustomerWallet.TotalCredit), totalCredit);
        dbContext.WalletTypes.Add(walletType);
        dbContext.CustomerWallets.Add(wallet);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();
    }

    private static void SetProperty<TTarget, TValue>(TTarget target, string propertyName, TValue value)
        where TTarget : class
    {
        typeof(TTarget).GetProperty(propertyName)!.SetValue(target, value);
    }
}

public sealed class CreditWalletEndpointTests : IClassFixture<GetCustomerWalletsApiFactory>
{
    private readonly GetCustomerWalletsApiFactory _factory;

    public CreditWalletEndpointTests(GetCustomerWalletsApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Credit_WhenRequestIsValid_ReturnsCreditResponse()
    {
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await CreditWalletServiceTests.SeedWalletAsync(
                dbContext,
                9501,
                951,
                true,
                true,
                5m,
                5m);
        }

        using var client = _factory.CreateClient();
        using var response = await client.PostAsJsonAsync(
            "/api/wallets/credit",
            new
            {
                CustomerId = 9501,
                WalletTypeId = 951,
                Amount = 20m,
                ReferenceType = "ORDER",
                ReferenceId = 55001,
                Remarks = "Endpoint credit"
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<CreditWalletResultDto>();
        Assert.NotNull(result);
        Assert.Equal(5m, result.BalanceBefore);
        Assert.Equal(25m, result.BalanceAfter);
        Assert.Equal("ORDER", result.ReferenceType);
    }

    [Fact]
    public async Task Credit_WhenAmountIsInvalid_ReturnsStandardValidationProblem()
    {
        using var client = _factory.CreateClient();
        using var response = await client.PostAsJsonAsync(
            "/api/wallets/credit",
            new { CustomerId = 9502, WalletTypeId = 952, Amount = 0m });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.Status);
        Assert.Equal("Validation failed", problem.Title);
    }

    [Fact]
    public async Task Credit_WhenUnauthenticated_ReturnsUnauthorized()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/wallets/credit",
            new { CustomerId = 9503, WalletTypeId = 953, Amount = 10m });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
