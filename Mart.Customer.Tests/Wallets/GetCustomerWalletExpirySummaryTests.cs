using System.Net;
using System.Net.Http.Json;
using FluentValidation.TestHelper;
using Mart.Customer.Application.Abstractions.Data;
using Mart.Customer.Application.Wallets.Dtos;
using Mart.Customer.Application.Wallets.Queries.GetCustomerWalletExpirySummary;
using Mart.Customer.Domain.Wallets;
using Mart.Customer.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Mart.Customer.Tests.Wallets;

public sealed class GetCustomerWalletExpirySummaryTests
    : IClassFixture<GetCustomerWalletsApiFactory>
{
    private readonly GetCustomerWalletsApiFactory _factory;

    public GetCustomerWalletExpirySummaryTests(GetCustomerWalletsApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetExpirySummary_WhenExpiringBalancesExist_ReturnsSqlAggregates()
    {
        var wallet = await SeedWalletAsync(9101, 911, isWalletTypeActive: true);
        var firstExpiry = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);
        await SeedBucketsAsync(
            CreateBucket(91101, wallet.CustomerWalletId, 1, 15.25m, firstExpiry),
            CreateBucket(91102, wallet.CustomerWalletId, 2, 4.75m, firstExpiry.AddDays(5)),
            CreateBucket(91103, wallet.CustomerWalletId, 3, 0m, firstExpiry.AddDays(-1)),
            CreateBucket(91104, wallet.CustomerWalletId, 4, 50m, null));

        using var client = _factory.CreateClient();
        using var response = await client.GetAsync(
            "/api/customers/9101/wallets/911/expiry-summary",
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var summary = await response.Content.ReadFromJsonAsync<WalletExpirySummaryDto>(
            CancellationToken.None);
        Assert.NotNull(summary);
        Assert.Equal(wallet.CustomerWalletId, summary.CustomerWalletId);
        Assert.Equal(9101, summary.CustomerId);
        Assert.Equal(911, summary.WalletTypeId);
        Assert.Equal(20m, summary.TotalExpiringAmount);
        Assert.Equal(firstExpiry, summary.NextExpiryDate);
        Assert.Equal(2, summary.ExpiringBucketCount);
    }

    [Fact]
    public async Task GetExpirySummary_WhenNoAvailableExpiringBuckets_ReturnsEmptySummary()
    {
        var wallet = await SeedWalletAsync(9201, 921, isWalletTypeActive: true);
        await SeedBucketsAsync(
            CreateBucket(92101, wallet.CustomerWalletId, 1, 0m, DateTime.UtcNow.AddDays(1)),
            CreateBucket(92102, wallet.CustomerWalletId, 2, 25m, null));

        using var client = _factory.CreateClient();
        using var response = await client.GetAsync(
            "/api/customers/9201/wallets/921/expiry-summary",
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var summary = await response.Content.ReadFromJsonAsync<WalletExpirySummaryDto>(
            CancellationToken.None);
        Assert.NotNull(summary);
        Assert.Equal(0m, summary.TotalExpiringAmount);
        Assert.Null(summary.NextExpiryDate);
        Assert.Equal(0, summary.ExpiringBucketCount);
    }

    [Fact]
    public async Task Repository_GetExpirySummaryAsync_DoesNotTrackWalletOrBucketRows()
    {
        await using var scope = CreateRepositoryScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        dbContext.WalletTypes.Add(CreateWalletType(931, true));
        var wallet = CustomerWallet.Create(9301, 931, DateTime.UtcNow);
        dbContext.CustomerWallets.Add(wallet);
        await dbContext.SaveChangesAsync(CancellationToken.None);
        dbContext.WalletBalanceBuckets.Add(
            CreateBucket(93101, wallet.CustomerWalletId, 1, 7m, DateTime.UtcNow.AddDays(1)));
        await dbContext.SaveChangesAsync(CancellationToken.None);
        dbContext.ChangeTracker.Clear();
        var repository = scope.ServiceProvider.GetRequiredService<IWalletBalanceBucketRepository>();

        var result = await repository.GetExpirySummaryAsync(9301, 931, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(7m, result.TotalExpiringAmount);
        Assert.Empty(dbContext.ChangeTracker.Entries());
    }

    [Fact]
    public async Task GetExpirySummary_WhenWalletTypeIsInactive_ReturnsNotFound()
    {
        await SeedWalletAsync(9401, 941, isWalletTypeActive: false);

        using var client = _factory.CreateClient();
        using var response = await client.GetAsync(
            "/api/customers/9401/wallets/941/expiry-summary",
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetExpirySummary_WhenIdentifiersAreInvalid_ReturnsStandardValidationProblem()
    {
        using var client = _factory.CreateClient();
        using var response = await client.GetAsync(
            "/api/customers/0/wallets/0/expiry-summary",
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(
            CancellationToken.None);
        Assert.NotNull(problem);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.Status);
        Assert.Equal("Validation failed", problem.Title);
    }

    [Fact]
    public void Validator_WhenIdentifiersAreNotPositive_ReturnsFailures()
    {
        var validator = new GetCustomerWalletExpirySummaryQueryValidator();

        var result = validator.TestValidate(new GetCustomerWalletExpirySummaryQuery(0, 0));

        result.ShouldHaveValidationErrorFor(query => query.CustomerId);
        result.ShouldHaveValidationErrorFor(query => query.WalletTypeId);
    }

    private async Task<CustomerWallet> SeedWalletAsync(
        long customerId,
        int walletTypeId,
        bool isWalletTypeActive)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        dbContext.WalletTypes.Add(CreateWalletType(walletTypeId, isWalletTypeActive));
        var wallet = CustomerWallet.Create(customerId, walletTypeId, DateTime.UtcNow);
        dbContext.CustomerWallets.Add(wallet);
        await dbContext.SaveChangesAsync(CancellationToken.None);
        return wallet;
    }

    private async Task SeedBucketsAsync(params WalletBalanceBucket[] buckets)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        dbContext.WalletBalanceBuckets.AddRange(buckets);
        await dbContext.SaveChangesAsync(CancellationToken.None);
    }

    private static AsyncServiceScope CreateRepositoryScope()
    {
        var services = new ServiceCollection();
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        var repositoryType = typeof(ApplicationDbContext).Assembly.GetType(
            "Mart.Customer.Persistence.Repositories.WalletBalanceBucketRepository",
            throwOnError: true)!;
        services.AddScoped(typeof(IWalletBalanceBucketRepository), repositoryType);
        return services.BuildServiceProvider().CreateAsyncScope();
    }

    private static WalletType CreateWalletType(int id, bool isActive)
    {
        var walletType = (WalletType)Activator.CreateInstance(typeof(WalletType), nonPublic: true)!;
        SetProperty(walletType, nameof(WalletType.Id), id);
        SetProperty(walletType, nameof(WalletType.Name), $"Wallet {id}");
        SetProperty(walletType, nameof(WalletType.Code), $"WALLET_{id}");
        SetProperty(walletType, nameof(WalletType.IsActive), isActive);
        SetProperty(walletType, nameof(WalletType.CreatedDate), DateTime.UtcNow);
        return walletType;
    }

    private static WalletBalanceBucket CreateBucket(
        long id,
        long customerWalletId,
        long sourceTransactionId,
        decimal availableAmount,
        DateTime? expiryDate)
    {
        var bucket = (WalletBalanceBucket)Activator.CreateInstance(
            typeof(WalletBalanceBucket),
            nonPublic: true)!;
        SetProperty(bucket, nameof(WalletBalanceBucket.WalletBalanceBucketId), id);
        SetProperty(bucket, nameof(WalletBalanceBucket.CustomerWalletId), customerWalletId);
        SetProperty(bucket, nameof(WalletBalanceBucket.SourceTransactionId), sourceTransactionId);
        SetProperty(bucket, nameof(WalletBalanceBucket.OriginalAmount), availableAmount);
        SetProperty(bucket, nameof(WalletBalanceBucket.AvailableAmount), availableAmount);
        SetProperty(bucket, nameof(WalletBalanceBucket.ExpiryDate), expiryDate);
        SetProperty(bucket, nameof(WalletBalanceBucket.CreatedOn), DateTime.UtcNow);
        return bucket;
    }

    private static void SetProperty<TTarget, TValue>(TTarget target, string propertyName, TValue value)
        where TTarget : class
    {
        typeof(TTarget).GetProperty(propertyName)!.SetValue(target, value);
    }
}
