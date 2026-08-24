using System.Net;
using System.Net.Http.Json;
using FluentValidation.TestHelper;
using Mart.Customer.Application.Abstractions.Data;
using Mart.Customer.Application.Wallets.Dtos;
using Mart.Customer.Application.Wallets.Queries.GetCustomerWalletBuckets;
using Mart.Customer.Domain.Wallets;
using Mart.Customer.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Mart.Customer.Tests.Wallets;

public sealed class GetCustomerWalletBucketsTests
    : IClassFixture<GetCustomerWalletsApiFactory>
{
    private readonly GetCustomerWalletsApiFactory _factory;

    public GetCustomerWalletBucketsTests(GetCustomerWalletsApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetBuckets_WhenAvailableOnlyAndSourceRequested_FiltersSortsAndIncludesSourceTransaction()
    {
        var wallet = await SeedWalletAsync(8101, 811, isWalletTypeActive: true);
        var now = new DateTime(2026, 8, 21, 10, 0, 0, DateTimeKind.Utc);
        var firstTransaction = CreateTransaction(81001, wallet.CustomerWalletId, "TX-81001", now);
        var secondTransaction = CreateTransaction(81002, wallet.CustomerWalletId, "TX-81002", now.AddMinutes(1));
        var thirdTransaction = CreateTransaction(81003, wallet.CustomerWalletId, "TX-81003", now.AddMinutes(2));
        var fourthTransaction = CreateTransaction(81004, wallet.CustomerWalletId, "TX-81004", now.AddMinutes(3));
        await SeedTransactionsAndBucketsAsync(
            [firstTransaction, secondTransaction, thirdTransaction, fourthTransaction],
            [
                CreateBucket(81101, wallet.CustomerWalletId, firstTransaction.WalletTransactionId, 10m, 10m, now.AddDays(10), now),
                CreateBucket(81102, wallet.CustomerWalletId, secondTransaction.WalletTransactionId, 20m, 0m, now.AddDays(1), now),
                CreateBucket(81103, wallet.CustomerWalletId, thirdTransaction.WalletTransactionId, 30m, 30m, null, now),
                CreateBucket(81104, wallet.CustomerWalletId, fourthTransaction.WalletTransactionId, 40m, 40m, now.AddDays(5), now)
            ]);

        using var client = _factory.CreateClient();
        using var response = await client.GetAsync(
            "/api/customers/8101/wallets/811/buckets?availableOnly=true&includeSourceTransaction=true",
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var buckets = await response.Content.ReadFromJsonAsync<List<WalletBalanceBucketDto>>(
            CancellationToken.None);
        Assert.Collection(
            Assert.IsType<List<WalletBalanceBucketDto>>(buckets),
            bucket =>
            {
                Assert.Equal(81104, bucket.WalletBalanceBucketId);
                Assert.Equal(now.AddDays(5), bucket.ExpiryDate);
                Assert.Equal("TX-81004", bucket.SourceTransaction?.TransactionNumber);
            },
            bucket =>
            {
                Assert.Equal(81101, bucket.WalletBalanceBucketId);
                Assert.Equal(now.AddDays(10), bucket.ExpiryDate);
                Assert.Equal("TX-81001", bucket.SourceTransaction?.TransactionNumber);
            },
            bucket =>
            {
                Assert.Equal(81103, bucket.WalletBalanceBucketId);
                Assert.Null(bucket.ExpiryDate);
                Assert.Equal("TX-81003", bucket.SourceTransaction?.TransactionNumber);
            });
    }

    [Fact]
    public async Task Repository_WhenSourceIsNotRequested_ReturnsAllBucketsWithoutTrackingOrSourceProjection()
    {
        await using var scope = CreateRepositoryScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var transaction = CreateTransaction(82001, 8201, "TX-82001", DateTime.UtcNow);
        dbContext.WalletTransactions.Add(transaction);
        dbContext.WalletBalanceBuckets.Add(CreateBucket(
            82101,
            8201,
            transaction.WalletTransactionId,
            5m,
            0m,
            null,
            DateTime.UtcNow));
        await dbContext.SaveChangesAsync(CancellationToken.None);
        dbContext.ChangeTracker.Clear();
        var repository = scope.ServiceProvider.GetRequiredService<IWalletBalanceBucketRepository>();

        var result = await repository.GetByCustomerWalletIdAsync(
            8201,
            availableOnly: false,
            includeSourceTransaction: false,
            CancellationToken.None);

        var bucket = Assert.Single(result);
        Assert.Equal(0m, bucket.AvailableAmount);
        Assert.Null(bucket.SourceTransaction);
        Assert.Empty(dbContext.ChangeTracker.Entries());
    }

    [Fact]
    public async Task GetBuckets_WhenWalletTypeIsInactive_ReturnsNotFound()
    {
        await SeedWalletAsync(8301, 831, isWalletTypeActive: false);

        using var client = _factory.CreateClient();
        using var response = await client.GetAsync(
            "/api/customers/8301/wallets/831/buckets",
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetBuckets_WhenIdentifierIsInvalid_ReturnsStandardValidationProblem()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync(
            "/api/customers/0/wallets/0/buckets",
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public void Validator_WhenIdentifiersAreNotPositive_ReturnsFailures()
    {
        var validator = new GetCustomerWalletBucketsQueryValidator();

        var result = validator.TestValidate(new GetCustomerWalletBucketsQuery(0, 0));

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

    private async Task SeedTransactionsAndBucketsAsync(
        IEnumerable<WalletTransaction> transactions,
        IEnumerable<WalletBalanceBucket> buckets)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        dbContext.WalletTransactions.AddRange(transactions);
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

    private static WalletTransaction CreateTransaction(
        long id,
        long customerWalletId,
        string transactionNumber,
        DateTime transactionDate)
    {
        var transaction = (WalletTransaction)Activator.CreateInstance(
            typeof(WalletTransaction),
            nonPublic: true)!;
        SetProperty(transaction, nameof(WalletTransaction.WalletTransactionId), id);
        SetProperty(transaction, nameof(WalletTransaction.CustomerWalletId), customerWalletId);
        SetProperty(transaction, nameof(WalletTransaction.TransactionNumber), transactionNumber);
        SetProperty(transaction, nameof(WalletTransaction.TransactionType), "CREDIT");
        SetProperty(transaction, nameof(WalletTransaction.Amount), 10m);
        SetProperty(transaction, nameof(WalletTransaction.BalanceBefore), 0m);
        SetProperty(transaction, nameof(WalletTransaction.BalanceAfter), 10m);
        SetProperty(transaction, nameof(WalletTransaction.TransactionDate), transactionDate);
        return transaction;
    }

    private static WalletBalanceBucket CreateBucket(
        long id,
        long customerWalletId,
        long sourceTransactionId,
        decimal originalAmount,
        decimal availableAmount,
        DateTime? expiryDate,
        DateTime createdOn)
    {
        var bucket = (WalletBalanceBucket)Activator.CreateInstance(
            typeof(WalletBalanceBucket),
            nonPublic: true)!;
        SetProperty(bucket, nameof(WalletBalanceBucket.WalletBalanceBucketId), id);
        SetProperty(bucket, nameof(WalletBalanceBucket.CustomerWalletId), customerWalletId);
        SetProperty(bucket, nameof(WalletBalanceBucket.SourceTransactionId), sourceTransactionId);
        SetProperty(bucket, nameof(WalletBalanceBucket.OriginalAmount), originalAmount);
        SetProperty(bucket, nameof(WalletBalanceBucket.AvailableAmount), availableAmount);
        SetProperty(bucket, nameof(WalletBalanceBucket.ExpiryDate), expiryDate);
        SetProperty(bucket, nameof(WalletBalanceBucket.CreatedOn), createdOn);
        return bucket;
    }

    private static void SetProperty<TTarget, TValue>(TTarget target, string propertyName, TValue value)
        where TTarget : class
    {
        typeof(TTarget).GetProperty(propertyName)!.SetValue(target, value);
    }
}
