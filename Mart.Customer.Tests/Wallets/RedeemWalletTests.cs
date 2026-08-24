using System.Net;
using System.Net.Http.Json;
using FluentValidation.TestHelper;
using Mart.Customer.Application.Abstractions.Data;
using Mart.Customer.Application.Wallets.Commands.RedeemWallet;
using Mart.Customer.Application.Wallets.Dtos;
using Mart.Customer.Domain.Common;
using Mart.Customer.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Mart.Customer.Tests.Wallets;

public sealed class RedeemWalletServiceTests
{
    [Fact]
    public async Task Handle_RedeemsDeterministicBucketsAndCreatesOneLedgerRow()
    {
        await using var scope = CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var now = DateTime.UtcNow;
        var wallet = await RedeemPreviewServiceTests.SeedWalletAsync(
            dbContext, 11101, 1111, true, true, 120m);
        RedeemPreviewServiceTests.SeedBuckets(
            dbContext,
            wallet.CustomerWalletId,
            RedeemPreviewServiceTests.CreateBucket(
                111101, wallet.CustomerWalletId, 30m, now.AddDays(10), now),
            RedeemPreviewServiceTests.CreateBucket(
                111102, wallet.CustomerWalletId, 20m, now.AddDays(5), now),
            RedeemPreviewServiceTests.CreateBucket(
                111103, wallet.CustomerWalletId, 25m, now.AddDays(5), now),
            RedeemPreviewServiceTests.CreateBucket(
                111104, wallet.CustomerWalletId, 50m, null, now),
            RedeemPreviewServiceTests.CreateBucket(
                111105, wallet.CustomerWalletId, 40m, now.AddDays(-1), now.AddDays(-10)));
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();
        var handler = scope.ServiceProvider.GetRequiredService<RedeemWalletCommandHandler>();

        var result = await handler.Handle(
            CreateCommand(11101, 1111, 60m, "order", 61101),
            CancellationToken.None);

        Assert.Equal(120m, result.BalanceBefore);
        Assert.Equal(60m, result.BalanceAfter);
        Assert.Equal(60m, result.TotalDebit);
        Assert.Equal("ORDER", result.ReferenceType);
        var savedWallet = await dbContext.CustomerWallets.SingleAsync();
        Assert.Equal(60m, savedWallet.CurrentBalance);
        Assert.Equal(60m, savedWallet.TotalDebit);
        var transaction = await dbContext.WalletTransactions.SingleAsync();
        Assert.Equal("REDEMPTION", transaction.TransactionType);
        Assert.Equal(60m, transaction.Amount);
        Assert.Equal(
            new[] { 15m, 0m, 0m, 50m, 40m },
            await dbContext.WalletBalanceBuckets
                .OrderBy(bucket => bucket.WalletBalanceBucketId)
                .Select(bucket => bucket.AvailableAmount)
                .ToArrayAsync());
    }

    [Fact]
    public async Task Handle_WhenEligibleBucketsAreInsufficient_DoesNotChangeAnyWalletData()
    {
        await using var scope = CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var now = DateTime.UtcNow;
        var wallet = await RedeemPreviewServiceTests.SeedWalletAsync(
            dbContext, 11201, 1121, true, true, 50m);
        RedeemPreviewServiceTests.SeedBuckets(
            dbContext,
            wallet.CustomerWalletId,
            RedeemPreviewServiceTests.CreateBucket(
                112101, wallet.CustomerWalletId, 10m, now.AddDays(1), now),
            RedeemPreviewServiceTests.CreateBucket(
                112102, wallet.CustomerWalletId, 40m, now.AddDays(-1), now.AddDays(-5)));
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();
        var handler = scope.ServiceProvider.GetRequiredService<RedeemWalletCommandHandler>();

        var exception = await Assert.ThrowsAsync<DomainException>(() => handler.Handle(
            CreateCommand(11201, 1121, 20m, null, null),
            CancellationToken.None));

        Assert.Equal("Insufficient wallet balance.", exception.Message);
        Assert.Equal(50m, (await dbContext.CustomerWallets.SingleAsync()).CurrentBalance);
        Assert.Equal(0m, (await dbContext.CustomerWallets.SingleAsync()).TotalDebit);
        Assert.Equal(
            new[] { 10m, 40m },
            await dbContext.WalletBalanceBuckets
                .OrderBy(bucket => bucket.WalletBalanceBucketId)
                .Select(bucket => bucket.AvailableAmount)
                .ToArrayAsync());
        Assert.Empty(await dbContext.WalletTransactions.ToListAsync());
    }

    [Fact]
    public async Task Handle_RepeatedReferenceReturnsOriginalRedemptionWithoutRedeemingAgain()
    {
        await using var scope = CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var wallet = await RedeemPreviewServiceTests.SeedWalletAsync(
            dbContext, 11301, 1131, true, true, 30m);
        RedeemPreviewServiceTests.SeedBuckets(
            dbContext,
            wallet.CustomerWalletId,
            RedeemPreviewServiceTests.CreateBucket(
                113101, wallet.CustomerWalletId, 30m, null, DateTime.UtcNow));
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();
        var handler = scope.ServiceProvider.GetRequiredService<RedeemWalletCommandHandler>();
        var command = CreateCommand(11301, 1131, 12m, "ORDER", 61301);

        var first = await handler.Handle(command, CancellationToken.None);
        var repeated = await handler.Handle(command, CancellationToken.None);

        Assert.Equal(first.WalletTransactionId, repeated.WalletTransactionId);
        Assert.Equal(first.TransactionNumber, repeated.TransactionNumber);
        Assert.Equal(18m, (await dbContext.CustomerWallets.SingleAsync()).CurrentBalance);
        Assert.Equal(12m, (await dbContext.CustomerWallets.SingleAsync()).TotalDebit);
        Assert.Equal(18m, (await dbContext.WalletBalanceBuckets.SingleAsync()).AvailableAmount);
        Assert.Equal(1, await dbContext.WalletTransactions.CountAsync());
    }

    [Fact]
    public async Task Handle_ReusedReferenceWithDifferentAmountIsRejectedWithoutChangingData()
    {
        await using var scope = CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var wallet = await RedeemPreviewServiceTests.SeedWalletAsync(
            dbContext, 11401, 1141, true, true, 30m);
        RedeemPreviewServiceTests.SeedBuckets(
            dbContext,
            wallet.CustomerWalletId,
            RedeemPreviewServiceTests.CreateBucket(
                114101, wallet.CustomerWalletId, 30m, null, DateTime.UtcNow));
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();
        var handler = scope.ServiceProvider.GetRequiredService<RedeemWalletCommandHandler>();
        await handler.Handle(
            CreateCommand(11401, 1141, 10m, "ORDER", 61401),
            CancellationToken.None);

        var exception = await Assert.ThrowsAsync<DomainException>(() => handler.Handle(
            CreateCommand(11401, 1141, 11m, "ORDER", 61401),
            CancellationToken.None));

        Assert.Contains("different redemption details", exception.Message);
        Assert.Equal(20m, (await dbContext.CustomerWallets.SingleAsync()).CurrentBalance);
        Assert.Equal(20m, (await dbContext.WalletBalanceBuckets.SingleAsync()).AvailableAmount);
        Assert.Equal(1, await dbContext.WalletTransactions.CountAsync());
    }

    [Theory]
    [InlineData(false, true, "Customer wallet is inactive.")]
    [InlineData(true, false, "Wallet type is inactive.")]
    public async Task Handle_WhenWalletOrTypeIsInactive_IsRejected(
        bool walletIsActive,
        bool walletTypeIsActive,
        string expectedMessage)
    {
        await using var scope = CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await RedeemPreviewServiceTests.SeedWalletAsync(
            dbContext,
            walletIsActive ? 11501 : 11502,
            walletIsActive ? 1151 : 1152,
            walletIsActive,
            walletTypeIsActive,
            20m);
        var handler = scope.ServiceProvider.GetRequiredService<RedeemWalletCommandHandler>();

        var exception = await Assert.ThrowsAsync<DomainException>(() => handler.Handle(
            CreateCommand(
                walletIsActive ? 11501 : 11502,
                walletIsActive ? 1151 : 1152,
                5m,
                null,
                null),
            CancellationToken.None));

        Assert.Equal(expectedMessage, exception.Message);
        Assert.Empty(await dbContext.WalletTransactions.ToListAsync());
    }

    [Fact]
    public void Validator_RejectsInvalidIdsAmountReferencePairAndCreatedBy()
    {
        var validator = new RedeemWalletCommandValidator();
        var result = validator.TestValidate(new RedeemWalletCommand(
            0, 0, 0.001m, "ORDER", null, null, string.Empty));

        result.ShouldHaveValidationErrorFor(command => command.CustomerId);
        result.ShouldHaveValidationErrorFor(command => command.WalletTypeId);
        result.ShouldHaveValidationErrorFor(command => command.Amount);
        result.ShouldHaveValidationErrorFor(command => command.ReferenceId);
        result.ShouldHaveValidationErrorFor(command => command.CreatedBy);
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
        services.AddScoped<RedeemWalletCommandHandler>();
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

    private static RedeemWalletCommand CreateCommand(
        long customerId,
        int walletTypeId,
        decimal amount,
        string? referenceType,
        long? referenceId)
    {
        return new RedeemWalletCommand(
            customerId,
            walletTypeId,
            amount,
            referenceType,
            referenceId,
            "Redemption test",
            "wallet-tests");
    }
}

public sealed class RedeemWalletEndpointTests : IClassFixture<GetCustomerWalletsApiFactory>
{
    private readonly GetCustomerWalletsApiFactory _factory;

    public RedeemWalletEndpointTests(GetCustomerWalletsApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Redeem_WhenRequestIsValid_ReturnsRedemptionResponse()
    {
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var wallet = await RedeemPreviewServiceTests.SeedWalletAsync(
                dbContext, 11601, 1161, true, true, 25m);
            RedeemPreviewServiceTests.SeedBuckets(
                dbContext,
                wallet.CustomerWalletId,
                RedeemPreviewServiceTests.CreateBucket(
                    116101, wallet.CustomerWalletId, 25m, null, DateTime.UtcNow));
            await dbContext.SaveChangesAsync();
        }

        using var client = _factory.CreateClient();
        using var response = await client.PostAsJsonAsync(
            "/api/wallets/redeem",
            new
            {
                CustomerId = 11601,
                WalletTypeId = 1161,
                Amount = 10m,
                ReferenceType = "ORDER",
                ReferenceId = 61601,
                Remarks = "Endpoint redemption"
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<RedeemWalletResultDto>();
        Assert.NotNull(result);
        Assert.Equal(25m, result.BalanceBefore);
        Assert.Equal(15m, result.BalanceAfter);
        Assert.Equal(10m, result.TotalDebit);
        Assert.Equal("ORDER", result.ReferenceType);
    }

    [Fact]
    public async Task Redeem_WhenAmountIsInvalid_ReturnsStandardValidationProblem()
    {
        using var client = _factory.CreateClient();
        using var response = await client.PostAsJsonAsync(
            "/api/wallets/redeem",
            new { CustomerId = 11602, WalletTypeId = 1162, Amount = 0m });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.Status);
        Assert.Equal("Validation failed", problem.Title);
    }

    [Fact]
    public async Task Redeem_WhenUnauthenticated_ReturnsUnauthorized()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/wallets/redeem",
            new { CustomerId = 11603, WalletTypeId = 1163, Amount = 10m });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
