using System.Net;
using System.Net.Http.Json;
using FluentValidation.TestHelper;
using Mart.Customer.Application.Abstractions.Data;
using Mart.Customer.Application.Wallets.Dtos;
using Mart.Customer.Application.Wallets.Queries.PreviewWalletRedemption;
using Mart.Customer.Domain.Common;
using Mart.Customer.Domain.Wallets;
using Mart.Customer.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Mart.Customer.Tests.Wallets;

public sealed class RedeemPreviewServiceTests
{
    [Fact]
    public async Task Handle_AllocatesByExpiryThenNonExpiringWithoutTrackingOrWriting()
    {
        await using var scope = CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var now = DateTime.UtcNow;
        var wallet = await SeedWalletAsync(dbContext, 10101, 1011, true, true, 120m);
        SeedBuckets(
            dbContext,
            wallet.CustomerWalletId,
            CreateBucket(101101, wallet.CustomerWalletId, 30m, now.AddDays(10), now),
            CreateBucket(101102, wallet.CustomerWalletId, 40m, now.AddDays(5), now),
            CreateBucket(101103, wallet.CustomerWalletId, 50m, null, now),
            CreateBucket(101104, wallet.CustomerWalletId, 25m, now.AddDays(-1), now.AddDays(-10)));
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();
        var handler = scope.ServiceProvider.GetRequiredService<PreviewWalletRedemptionQueryHandler>();

        var result = await handler.Handle(
            new PreviewWalletRedemptionQuery(10101, 1011, 60m),
            CancellationToken.None);

        Assert.Equal(120m, result.BalanceBefore);
        Assert.Equal(60m, result.BalanceAfter);
        Assert.Collection(
            result.BucketAllocations,
            allocation =>
            {
                Assert.Equal(101102, allocation.WalletBalanceBucketId);
                Assert.Equal(40m, allocation.RedeemAmount);
                Assert.Equal(0m, allocation.AvailableAmountAfter);
            },
            allocation =>
            {
                Assert.Equal(101101, allocation.WalletBalanceBucketId);
                Assert.Equal(20m, allocation.RedeemAmount);
                Assert.Equal(10m, allocation.AvailableAmountAfter);
            });
        Assert.Empty(dbContext.ChangeTracker.Entries());
        Assert.Empty(await dbContext.WalletTransactions.AsNoTracking().ToListAsync());
        Assert.Equal(120m, (await dbContext.CustomerWallets.AsNoTracking().SingleAsync()).CurrentBalance);
        Assert.Equal(
            new[] { 30m, 40m, 50m, 25m },
            await dbContext.WalletBalanceBuckets
                .AsNoTracking()
                .OrderBy(bucket => bucket.WalletBalanceBucketId)
                .Select(bucket => bucket.AvailableAmount)
                .ToArrayAsync());
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
        await SeedWalletAsync(
            dbContext,
            walletIsActive ? 10201 : 10202,
            walletIsActive ? 1021 : 1022,
            walletIsActive,
            walletTypeIsActive,
            20m);
        var handler = scope.ServiceProvider.GetRequiredService<PreviewWalletRedemptionQueryHandler>();

        var exception = await Assert.ThrowsAsync<DomainException>(() => handler.Handle(
            new PreviewWalletRedemptionQuery(
                walletIsActive ? 10201 : 10202,
                walletIsActive ? 1021 : 1022,
                5m),
            CancellationToken.None));

        Assert.Equal(expectedMessage, exception.Message);
    }

    [Fact]
    public async Task Handle_WhenOnlyExpiredBucketsCoverAmount_IsRejected()
    {
        await using var scope = CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var now = DateTime.UtcNow;
        var wallet = await SeedWalletAsync(dbContext, 10301, 1031, true, true, 50m);
        SeedBuckets(
            dbContext,
            wallet.CustomerWalletId,
            CreateBucket(103101, wallet.CustomerWalletId, 10m, now.AddDays(1), now),
            CreateBucket(103102, wallet.CustomerWalletId, 40m, now.AddDays(-1), now.AddDays(-5)));
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();
        var handler = scope.ServiceProvider.GetRequiredService<PreviewWalletRedemptionQueryHandler>();

        var exception = await Assert.ThrowsAsync<DomainException>(() => handler.Handle(
            new PreviewWalletRedemptionQuery(10301, 1031, 20m),
            CancellationToken.None));

        Assert.Equal("Insufficient wallet balance.", exception.Message);
        Assert.Empty(dbContext.ChangeTracker.Entries());
    }

    [Fact]
    public void Validator_RejectsInvalidIdentifiersAndAmount()
    {
        var validator = new PreviewWalletRedemptionQueryValidator();

        var result = validator.TestValidate(
            new PreviewWalletRedemptionQuery(0, 0, 0.001m));

        result.ShouldHaveValidationErrorFor(query => query.CustomerId);
        result.ShouldHaveValidationErrorFor(query => query.WalletTypeId);
        result.ShouldHaveValidationErrorFor(query => query.Amount);
    }

    private static AsyncServiceScope CreateScope()
    {
        var services = new ServiceCollection();
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        RegisterInternalRepository<ICustomerWalletRepository>(services, "CustomerWalletRepository");
        RegisterInternalRepository<IWalletBalanceBucketRepository>(services, "WalletBalanceBucketRepository");
        services.AddScoped<PreviewWalletRedemptionQueryHandler>();
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

    internal static async Task<CustomerWallet> SeedWalletAsync(
        ApplicationDbContext dbContext,
        long customerId,
        int walletTypeId,
        bool walletIsActive,
        bool walletTypeIsActive,
        decimal balance)
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
        SetProperty(wallet, nameof(CustomerWallet.TotalCredit), balance);
        dbContext.WalletTypes.Add(walletType);
        dbContext.CustomerWallets.Add(wallet);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();
        return wallet;
    }

    internal static WalletBalanceBucket CreateBucket(
        long id,
        long customerWalletId,
        decimal availableAmount,
        DateTime? expiryDate,
        DateTime createdOn)
    {
        var bucket = (WalletBalanceBucket)Activator.CreateInstance(
            typeof(WalletBalanceBucket),
            nonPublic: true)!;
        SetProperty(bucket, nameof(WalletBalanceBucket.WalletBalanceBucketId), id);
        SetProperty(bucket, nameof(WalletBalanceBucket.CustomerWalletId), customerWalletId);
        SetProperty(bucket, nameof(WalletBalanceBucket.SourceTransactionId), id + 100000);
        SetProperty(bucket, nameof(WalletBalanceBucket.OriginalAmount), availableAmount);
        SetProperty(bucket, nameof(WalletBalanceBucket.AvailableAmount), availableAmount);
        SetProperty(bucket, nameof(WalletBalanceBucket.ExpiryDate), expiryDate);
        SetProperty(bucket, nameof(WalletBalanceBucket.CreatedOn), createdOn);
        return bucket;
    }

    internal static void SeedBuckets(
        ApplicationDbContext dbContext,
        long customerWalletId,
        params WalletBalanceBucket[] buckets)
    {
        Assert.All(buckets, bucket => Assert.Equal(customerWalletId, bucket.CustomerWalletId));
        dbContext.WalletBalanceBuckets.AddRange(buckets);
    }

    private static void SetProperty<TTarget, TValue>(
        TTarget target,
        string propertyName,
        TValue value)
        where TTarget : class
    {
        typeof(TTarget).GetProperty(propertyName)!.SetValue(target, value);
    }
}

public sealed class RedeemPreviewEndpointTests : IClassFixture<GetCustomerWalletsApiFactory>
{
    private readonly GetCustomerWalletsApiFactory _factory;

    public RedeemPreviewEndpointTests(GetCustomerWalletsApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Preview_WhenRequestIsValid_ReturnsAllocation()
    {
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var wallet = await RedeemPreviewServiceTests.SeedWalletAsync(
                dbContext,
                10401,
                1041,
                true,
                true,
                30m);
            RedeemPreviewServiceTests.SeedBuckets(
                dbContext,
                wallet.CustomerWalletId,
                RedeemPreviewServiceTests.CreateBucket(
                    104101,
                    wallet.CustomerWalletId,
                    30m,
                    null,
                    DateTime.UtcNow));
            await dbContext.SaveChangesAsync();
        }

        using var client = _factory.CreateClient();
        using var response = await client.PostAsJsonAsync(
            "/api/wallets/redeem-preview",
            new { CustomerId = 10401, WalletTypeId = 1041, Amount = 12m });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<RedeemPreviewResultDto>();
        Assert.NotNull(result);
        Assert.Equal(18m, result.BalanceAfter);
        Assert.Equal(12m, Assert.Single(result.BucketAllocations).RedeemAmount);
    }

    [Fact]
    public async Task Preview_WhenAmountIsInvalid_ReturnsStandardValidationProblem()
    {
        using var client = _factory.CreateClient();
        using var response = await client.PostAsJsonAsync(
            "/api/wallets/redeem-preview",
            new { CustomerId = 10402, WalletTypeId = 1042, Amount = 0m });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.Status);
        Assert.Equal("Validation failed", problem.Title);
    }

    [Fact]
    public async Task Preview_WhenUnauthenticated_ReturnsUnauthorized()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/wallets/redeem-preview",
            new { CustomerId = 10403, WalletTypeId = 1043, Amount = 10m });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
