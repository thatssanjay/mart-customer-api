using System.Net;
using System.Net.Http.Json;
using FluentValidation.TestHelper;
using Mart.Customer.Application.Abstractions.Data;
using Mart.Customer.Application.Wallets.Commands.RefundWallet;
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

public sealed class RefundWalletServiceTests
{
    [Fact]
    public async Task Handle_RefundsRedemptionAndCreatesCompensatingLedgerAndBucket()
    {
        await using var scope = CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var original = await SeedRefundableRedemptionAsync(
            dbContext, 12101, 1211, "TX-ORIGINAL-12101", 40m, 10m);
        var handler = scope.ServiceProvider.GetRequiredService<RefundWalletCommandHandler>();

        var result = await handler.Handle(
            CreateCommand(12101, 1211, original.TransactionNumber, 15m),
            CancellationToken.None);

        Assert.Equal(original.WalletTransactionId, result.OriginalWalletTransactionId);
        Assert.Equal(10m, result.BalanceBefore);
        Assert.Equal(25m, result.BalanceAfter);
        Assert.Equal(15m, result.RefundedAmount);
        Assert.Equal(25m, result.RemainingRefundableAmount);

        var wallet = await dbContext.CustomerWallets.SingleAsync();
        Assert.Equal(25m, wallet.CurrentBalance);
        var refund = await dbContext.WalletTransactions.SingleAsync(
            transaction => transaction.TransactionType == "REFUND");
        Assert.Equal(WalletTransaction.RefundReferenceType, refund.ReferenceType);
        Assert.Equal(original.WalletTransactionId, refund.ReferenceId);
        Assert.Equal(15m, refund.Amount);
        var bucket = await dbContext.WalletBalanceBuckets.SingleAsync();
        Assert.Equal(refund.WalletTransactionId, bucket.SourceTransactionId);
        Assert.Equal(15m, bucket.OriginalAmount);
        Assert.Equal(15m, bucket.AvailableAmount);
        Assert.Null(bucket.ExpiryDate);
    }

    [Fact]
    public async Task Handle_AllowsPartialRefundsUpToOriginalAmount()
    {
        await using var scope = CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var original = await SeedRefundableRedemptionAsync(
            dbContext, 12201, 1221, "TX-ORIGINAL-12201", 30m, 5m);
        var handler = scope.ServiceProvider.GetRequiredService<RefundWalletCommandHandler>();

        await handler.Handle(
            CreateCommand(12201, 1221, original.TransactionNumber, 12m),
            CancellationToken.None);
        var result = await handler.Handle(
            CreateCommand(12201, 1221, original.TransactionNumber, 18m),
            CancellationToken.None);

        Assert.Equal(30m, result.RefundedAmount);
        Assert.Equal(0m, result.RemainingRefundableAmount);
        Assert.Equal(35m, (await dbContext.CustomerWallets.SingleAsync()).CurrentBalance);
        Assert.Equal(2, await dbContext.WalletTransactions.CountAsync(
            transaction => transaction.TransactionType == "REFUND"));
        Assert.Equal(2, await dbContext.WalletBalanceBuckets.CountAsync());
    }

    [Fact]
    public async Task Handle_WhenRefundExceedsRemainingAmount_DoesNotChangeWalletData()
    {
        await using var scope = CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var original = await SeedRefundableRedemptionAsync(
            dbContext, 12301, 1231, "TX-ORIGINAL-12301", 20m, 5m);
        var handler = scope.ServiceProvider.GetRequiredService<RefundWalletCommandHandler>();
        await handler.Handle(
            CreateCommand(12301, 1231, original.TransactionNumber, 8m),
            CancellationToken.None);

        var exception = await Assert.ThrowsAsync<DomainException>(() => handler.Handle(
            CreateCommand(12301, 1231, original.TransactionNumber, 13m),
            CancellationToken.None));

        Assert.Equal("Refund amount exceeds the remaining refundable amount.", exception.Message);
        Assert.Equal(13m, (await dbContext.CustomerWallets.SingleAsync()).CurrentBalance);
        Assert.Single(await dbContext.WalletTransactions
            .Where(transaction => transaction.TransactionType == "REFUND")
            .ToListAsync());
        Assert.Single(await dbContext.WalletBalanceBuckets.ToListAsync());
    }

    [Fact]
    public async Task Handle_WhenOriginalTransactionBelongsToAnotherWallet_IsRejected()
    {
        await using var scope = CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var original = await SeedRefundableRedemptionAsync(
            dbContext, 12401, 1241, "TX-ORIGINAL-12401", 20m, 5m);
        await CreditWalletServiceTests.SeedWalletAsync(
            dbContext, 12402, 1242, true, true, 7m, 7m);
        var handler = scope.ServiceProvider.GetRequiredService<RefundWalletCommandHandler>();

        var exception = await Assert.ThrowsAsync<DomainException>(() => handler.Handle(
            CreateCommand(12402, 1242, original.TransactionNumber, 5m),
            CancellationToken.None));

        Assert.Equal(
            "Original transaction does not belong to the specified customer wallet.",
            exception.Message);
        var otherWallet = await dbContext.CustomerWallets.SingleAsync(wallet => wallet.CustomerId == 12402);
        Assert.Equal(7m, otherWallet.CurrentBalance);
        Assert.Empty(await dbContext.WalletBalanceBuckets.ToListAsync());
    }

    [Fact]
    public async Task Handle_WhenOriginalTransactionIsNotRedemption_IsRejected()
    {
        await using var scope = CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await CreditWalletServiceTests.SeedWalletAsync(
            dbContext, 12501, 1251, true, true, 10m, 10m);
        var wallet = await dbContext.CustomerWallets.SingleAsync();
        var credit = WalletTransaction.CreateCredit(
            "TX-CREDIT-12501",
            wallet.CustomerWalletId,
            10m,
            0m,
            10m,
            null,
            null,
            null,
            DateTime.UtcNow,
            "wallet-tests");
        dbContext.WalletTransactions.Add(credit);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();
        var handler = scope.ServiceProvider.GetRequiredService<RefundWalletCommandHandler>();

        var exception = await Assert.ThrowsAsync<DomainException>(() => handler.Handle(
            CreateCommand(12501, 1251, credit.TransactionNumber, 5m),
            CancellationToken.None));

        Assert.Equal("Only redemption transactions can be refunded.", exception.Message);
        Assert.Equal(10m, (await dbContext.CustomerWallets.SingleAsync()).CurrentBalance);
        Assert.Empty(await dbContext.WalletBalanceBuckets.ToListAsync());
    }

    [Fact]
    public void Validator_RejectsInvalidIdsTransactionNumberAmountAndCreatedBy()
    {
        var validator = new RefundWalletCommandValidator();
        var result = validator.TestValidate(new RefundWalletCommand(
            0, 0, string.Empty, 0.001m, null, string.Empty));

        result.ShouldHaveValidationErrorFor(command => command.CustomerId);
        result.ShouldHaveValidationErrorFor(command => command.WalletTypeId);
        result.ShouldHaveValidationErrorFor(command => command.OriginalTransactionNumber);
        result.ShouldHaveValidationErrorFor(command => command.Amount);
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
        services.AddScoped<RefundWalletCommandHandler>();
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

    private static RefundWalletCommand CreateCommand(
        long customerId,
        int walletTypeId,
        string originalTransactionNumber,
        decimal amount)
    {
        return new RefundWalletCommand(
            customerId,
            walletTypeId,
            originalTransactionNumber,
            amount,
            "Refund test",
            "wallet-tests");
    }

    internal static async Task<WalletTransaction> SeedRefundableRedemptionAsync(
        ApplicationDbContext dbContext,
        long customerId,
        int walletTypeId,
        string transactionNumber,
        decimal redemptionAmount,
        decimal currentBalance)
    {
        await CreditWalletServiceTests.SeedWalletAsync(
            dbContext,
            customerId,
            walletTypeId,
            true,
            true,
            currentBalance,
            currentBalance + redemptionAmount);
        var wallet = await dbContext.CustomerWallets.SingleAsync(
            candidate => candidate.CustomerId == customerId);
        var transaction = WalletTransaction.CreateRedemption(
            transactionNumber,
            wallet.CustomerWalletId,
            redemptionAmount,
            currentBalance + redemptionAmount,
            currentBalance,
            "ORDER",
            customerId,
            "Original redemption",
            DateTime.UtcNow.AddMinutes(-1),
            "wallet-tests");
        dbContext.WalletTransactions.Add(transaction);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();
        return transaction;
    }
}

public sealed class RefundWalletEndpointTests : IClassFixture<GetCustomerWalletsApiFactory>
{
    private readonly GetCustomerWalletsApiFactory _factory;

    public RefundWalletEndpointTests(GetCustomerWalletsApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Refund_WhenRequestIsValid_ReturnsRefundResponse()
    {
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await RefundWalletServiceTests.SeedRefundableRedemptionAsync(
                dbContext, 12601, 1261, "TX-ORIGINAL-12601", 25m, 5m);
        }

        using var client = _factory.CreateClient();
        using var response = await client.PostAsJsonAsync(
            "/api/wallets/refund",
            new
            {
                CustomerId = 12601,
                WalletTypeId = 1261,
                OriginalTransactionNumber = "TX-ORIGINAL-12601",
                Amount = 10m,
                Remarks = "Endpoint refund"
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<RefundWalletResultDto>();
        Assert.NotNull(result);
        Assert.Equal(5m, result.BalanceBefore);
        Assert.Equal(15m, result.BalanceAfter);
        Assert.Equal(15m, result.RemainingRefundableAmount);
    }

    [Fact]
    public async Task Refund_WhenAmountIsInvalid_ReturnsStandardValidationProblem()
    {
        using var client = _factory.CreateClient();
        using var response = await client.PostAsJsonAsync(
            "/api/wallets/refund",
            new
            {
                CustomerId = 12602,
                WalletTypeId = 1262,
                OriginalTransactionNumber = "TX-ORIGINAL-12602",
                Amount = 0m
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.Status);
        Assert.Equal("Validation failed", problem.Title);
    }

    [Fact]
    public async Task Refund_WhenUnauthenticated_ReturnsUnauthorized()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/wallets/refund",
            new
            {
                CustomerId = 12603,
                WalletTypeId = 1263,
                OriginalTransactionNumber = "TX-ORIGINAL-12603",
                Amount = 10m
            });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
