using System.Text.Json;
using FluentValidation;
using Mart.Customer.Application;
using Mart.Customer.Application.Abstractions.Data;
using Mart.Customer.Application.Wallets.Engine;
using Mart.Customer.Domain.Common;
using Mart.Customer.Domain.Wallets;
using Mart.Customer.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Mart.Customer.Tests.Wallets;

public sealed class WalletEngineFoundationTests
{
    internal static readonly DateTime EffectiveAt = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    internal static WalletAllocationResult Allocation(int type = 1, decimal amount = 10m,
        string component = WalletComponentCodes.BaseReward, long? store = null) =>
        new(type, store, component, amount, EffectiveAt.AddDays(30),
            new WalletCalculationSnapshot { ConfigurationId = 123, PointPercentage = 5m,
                RoundedAmount = amount, RoundingPolicy = WalletRoundingPolicy.Code });

    internal static WalletPostingRequest Request(params WalletAllocationResult[] allocations) =>
        new(WalletOperationKinds.SaleReward, WalletOperation.SaleBusinessKey(100), 7, 11,
            100, 200, EffectiveAt, "INTERNAL_POSTING_V1", "engine-tests", allocations);

    [Fact]
    public async Task Credit_PostsSeparateComponentsAndExactLedgerBucketInvariants()
    {
        await using var fixture = await WalletEngineFixture.CreateAsync();
        await fixture.SeedTypesAsync();
        var result = await fixture.Engine.PostAsync(Request(Allocation(),
            Allocation(amount: 2.25m, component: WalletComponentCodes.SubscriptionBonus), Allocation(2, 7m, store: 11)));

        Assert.False(result.IsIdempotentReplay);
        Assert.Equal(WalletOperationOutcomes.Credited, result.Outcome);
        Assert.Equal(3, result.Allocations.Count);
        var wallet = await fixture.Db.CustomerWallets.AsNoTracking().SingleAsync(x => x.WalletTypeId == 1);
        Assert.Equal(12.25m, wallet.CurrentBalance);
        Assert.Equal(12.25m, wallet.TotalCredit);
        Assert.Equal(0m, wallet.TotalDebit);
        Assert.Null(wallet.StoreId);
        Assert.Equal(2, await fixture.Db.CustomerWallets.CountAsync());
        Assert.Equal(3, await fixture.Db.WalletTransactions.CountAsync());
        Assert.Equal(3, await fixture.Db.WalletBalanceBuckets.CountAsync());
        foreach (var allocation in result.Allocations)
        {
            Assert.Equal(allocation.BalanceBefore + allocation.Amount, allocation.BalanceAfter);
            var bucket = await fixture.Db.WalletBalanceBuckets.AsNoTracking()
                .SingleAsync(x => x.SourceTransactionId == allocation.WalletTransactionId);
            Assert.Equal(allocation.Amount, bucket.OriginalAmount);
            Assert.Equal(allocation.Amount, bucket.AvailableAmount);
            Assert.Equal(EffectiveAt.AddDays(30), bucket.ExpiryDate);
            var transaction = await fixture.Db.WalletTransactions.AsNoTracking()
                .SingleAsync(x => x.WalletTransactionId == allocation.WalletTransactionId);
            Assert.Equal(EffectiveAt, transaction.TransactionDate);
            Assert.Equal("engine-tests", transaction.CreatedBy);
        }
        Assert.Equal(0m, result.Allocations[0].BalanceBefore);
        Assert.Equal(10m, result.Allocations[1].BalanceBefore);
        var snapshot = JsonSerializer.Deserialize<WalletCalculationSnapshot>(
            (await fixture.Db.WalletOperationComponents.AsNoTracking().FirstAsync()).CalculationSnapshotJson)!;
        Assert.Equal(123, snapshot.ConfigurationId);
        Assert.Equal(10m, snapshot.RoundedAmount);
    }

    [Fact]
    public async Task Replay_FromFreshScopeReturnsPersistedAmountsEvenAfterLaterCreditsAndNewMetadata()
    {
        await using var fixture = await WalletEngineFixture.CreateAsync();
        await fixture.SeedTypesAsync();
        var first = await fixture.Engine.PostAsync(Request(Allocation()));
        await fixture.Engine.PostAsync(Request(Allocation(amount: 4m)) with
        { CustomerOrderId = 101, BusinessKey = WalletOperation.SaleBusinessKey(101) });
        await using var scope = fixture.CreateScope();
        var replay = await scope.ServiceProvider.GetRequiredService<IWalletEngineService>()
            .PostAsync(Request(Allocation(amount: 999m)) with { CalculationVersion = "CHANGED" });
        Assert.True(replay.IsIdempotentReplay);
        Assert.Equal(first.WalletOperationId, replay.WalletOperationId);
        Assert.Equal(first.OperationNumber, replay.OperationNumber);
        Assert.Equal(first.EffectiveAt, replay.EffectiveAt);
        Assert.Equal(first.Allocations.ToArray(), replay.Allocations.ToArray());
        Assert.Equal(2, await fixture.Db.WalletTransactions.CountAsync());
        Assert.Equal(2, await fixture.Db.WalletBalanceBuckets.CountAsync());
        Assert.Equal(14m, await fixture.Db.CustomerWallets.AsNoTracking().Select(x => x.CurrentBalance).SingleAsync());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Resolver_MartRequiresPositiveStore(long store)
    {
        await using var fixture = await WalletEngineFixture.CreateAsync();
        await fixture.SeedTypesAsync();
        await Assert.ThrowsAsync<DomainException>(() => fixture.UnitOfWork.ExecuteInTransactionAsync(
            token => fixture.Resolver.ResolveAsync(7, 2, store == 0 ? null : store, token)));
        Assert.Empty(await fixture.Db.CustomerWallets.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task Resolver_NormalizesGlobalScopeAndReusesCanonicalWallets()
    {
        await using var fixture = await WalletEngineFixture.CreateAsync();
        await fixture.SeedTypesAsync();
        await fixture.UnitOfWork.ExecuteInTransactionAsync(async token =>
        {
            var global = await fixture.Resolver.ResolveAsync(7, 1, 11, token);
            Assert.Null(global.StoreId);
            Assert.Equal(global.CustomerWalletId, (await fixture.Resolver.ResolveAsync(7, 1, 99, token)).CustomerWalletId);
            var first = await fixture.Resolver.ResolveAsync(7, 2, 11, token);
            var second = await fixture.Resolver.ResolveAsync(7, 2, 12, token);
            Assert.NotEqual(first.CustomerWalletId, second.CustomerWalletId);
            Assert.Equal(first.CustomerWalletId, (await fixture.Resolver.ResolveAsync(7, 2, 11, token)).CustomerWalletId);
            return true;
        });
        Assert.Equal(3, await fixture.Db.CustomerWallets.CountAsync());
    }

    [Fact]
    public async Task Resolver_InactiveExistingWalletIsRejectedWithoutReplacement()
    {
        await using var fixture = await WalletEngineFixture.CreateAsync();
        await fixture.SeedTypesAsync();
        var wallet = CustomerWallet.Create(7, 1, EffectiveAt);
        wallet.UpdateStatus(false, EffectiveAt);
        fixture.Db.CustomerWallets.Add(wallet);
        await fixture.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<DomainException>(() => fixture.Engine.PostAsync(Request(Allocation())));
        Assert.Equal(1, await fixture.Db.CustomerWallets.CountAsync());
        Assert.Empty(await fixture.Db.WalletOperations.ToListAsync());
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("0.001")]
    [InlineData("10000000000000000")]
    public async Task InvalidAmountProducesNoFinancialRows(string amountText)
    {
        await using var fixture = await WalletEngineFixture.CreateAsync();
        await fixture.SeedTypesAsync();
        var amount = decimal.Parse(amountText, System.Globalization.CultureInfo.InvariantCulture);
        await Assert.ThrowsAsync<DomainException>(() => fixture.Engine.PostAsync(Request(Allocation(amount: amount))));
        Assert.Empty(await fixture.Db.WalletOperations.ToListAsync());
        Assert.Empty(await fixture.Db.WalletTransactions.ToListAsync());
    }

    [Fact]
    public async Task ZeroOnlyOperationPersistsNoRewardAndReplaysWithoutCreatingWallets()
    {
        await using var fixture = await WalletEngineFixture.CreateAsync();
        await fixture.SeedTypesAsync();
        var result = await fixture.Engine.PostAsync(Request(Allocation(amount: 0)));
        var replay = await fixture.Engine.PostAsync(Request(Allocation(amount: 10)));
        Assert.Equal(WalletOperationOutcomes.NoReward, result.Outcome);
        Assert.Equal(WalletOperationOutcomes.NoReward, replay.Outcome);
        Assert.True(replay.IsIdempotentReplay);
        Assert.Empty(result.Allocations);
        Assert.Equal(1, await fixture.Db.WalletOperations.CountAsync());
        Assert.Empty(await fixture.Db.CustomerWallets.ToListAsync());
        Assert.Empty(await fixture.Db.WalletTransactions.ToListAsync());
        Assert.Empty(await fixture.Db.WalletBalanceBuckets.ToListAsync());
    }

    [Fact]
    public async Task DuplicateCanonicalComponentIsRejectedIncludingGlobalStoreAliases()
    {
        await using var fixture = await WalletEngineFixture.CreateAsync();
        await fixture.SeedTypesAsync();
        await Assert.ThrowsAsync<DomainException>(() => fixture.Engine.PostAsync(Request(
            Allocation(store: 11), Allocation(store: 12))));
        Assert.Empty(await fixture.Db.WalletOperations.ToListAsync());
    }

    [Fact]
    public async Task DistinctAllocationKeysAllowTwoBaseComponentsOnOneWallet()
    {
        await using var fixture = await WalletEngineFixture.CreateAsync();
        await fixture.SeedTypesAsync();
        var result = await fixture.Engine.PostAsync(Request(Allocation() with { AllocationKey = "A" },
            Allocation() with { AllocationKey = "B" }));
        Assert.Equal(2, result.Allocations.Count);
        Assert.Equal(20m, result.Allocations[1].BalanceAfter);
    }

    [Fact]
    public async Task FailureOnSecondAllocationRollsBackSavedFirstPostingAndOperation()
    {
        await using var fixture = await WalletEngineFixture.CreateAsync();
        await fixture.SeedTypesAsync();
        var first = CustomerWallet.Create(7, 1, EffectiveAt);
        var second = CustomerWallet.Create(7, 2, EffectiveAt, 11);
        second.UpdateStatus(false, EffectiveAt);
        fixture.Db.CustomerWallets.AddRange(first, second);
        await fixture.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<DomainException>(() => fixture.Engine.PostAsync(Request(Allocation(), Allocation(2, store: 11))));
        Assert.Empty(await fixture.Db.WalletOperations.AsNoTracking().ToListAsync());
        Assert.Empty(await fixture.Db.WalletOperationComponents.AsNoTracking().ToListAsync());
        Assert.Empty(await fixture.Db.WalletTransactions.AsNoTracking().ToListAsync());
        Assert.Empty(await fixture.Db.WalletBalanceBuckets.AsNoTracking().ToListAsync());
        Assert.All(await fixture.Db.CustomerWallets.AsNoTracking().ToListAsync(), x =>
        { Assert.Equal(0m, x.CurrentBalance); Assert.Equal(0m, x.TotalCredit); });
        Assert.Empty(fixture.Db.ChangeTracker.Entries());
    }

    [Fact]
    public async Task FailedNewWalletPostingLeavesNoNewWalletBehind()
    {
        await using var fixture = await WalletEngineFixture.CreateAsync();
        await fixture.SeedTypesAsync();
        var second = CustomerWallet.Create(7, 2, EffectiveAt, 11);
        second.UpdateStatus(false, EffectiveAt);
        fixture.Db.CustomerWallets.Add(second);
        await fixture.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<DomainException>(() => fixture.Engine.PostAsync(Request(Allocation(), Allocation(2))));
        Assert.Single(await fixture.Db.CustomerWallets.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task EngineRejectsDirtyAndAmbientContextsWithoutDiscardingCallerChanges()
    {
        await using var fixture = await WalletEngineFixture.CreateAsync();
        await fixture.SeedTypesAsync();
        fixture.Db.CustomerWallets.Add(CustomerWallet.Create(7, 1, EffectiveAt));
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Engine.PostAsync(Request(Allocation())));
        Assert.True(fixture.Db.ChangeTracker.HasChanges());
        fixture.Db.ChangeTracker.Clear();
        await using var transaction = await fixture.Db.Database.BeginTransactionAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Engine.PostAsync(Request(Allocation())));
    }

    [Fact]
    public async Task DirectLedgerRejectsZeroAndNegativeAndResolverRequiresTransaction()
    {
        await using var fixture = await WalletEngineFixture.CreateAsync();
        await fixture.SeedTypesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Resolver.ResolveAsync(7, 1, null));
        await fixture.UnitOfWork.ExecuteInTransactionAsync(async token =>
        {
            var wallet = await fixture.Resolver.ResolveAsync(7, 1, null, token);
            var operation = WalletOperation.CreateSaleReward("TEST", WalletOperation.SaleBusinessKey(100),
                7, 11, 100, 200, EffectiveAt, "V1", "test", DateTime.UtcNow);
            fixture.Db.WalletOperations.Add(operation);
            await fixture.Db.SaveChangesAsync(token);
            var ledger = fixture.Services.GetRequiredService<IWalletLedgerService>();
            await Assert.ThrowsAsync<DomainException>(() => ledger.PostCreditAsync(operation, wallet, Allocation(amount: 0), token));
            await Assert.ThrowsAsync<DomainException>(() => ledger.PostCreditAsync(operation, wallet, Allocation(amount: -1), token));
            Assert.Equal(0m, wallet.CurrentBalance);
            operation.Complete(false, DateTime.UtcNow);
            await fixture.Db.SaveChangesAsync(token);
            return true;
        });
        Assert.Empty(await fixture.Db.WalletTransactions.ToListAsync());
    }

    [Fact]
    public async Task ReplayRejectsDifferentSourceIdentity()
    {
        await using var fixture = await WalletEngineFixture.CreateAsync();
        await fixture.SeedTypesAsync();
        await fixture.Engine.PostAsync(Request(Allocation()));
        await Assert.ThrowsAsync<DomainException>(() => fixture.Engine.PostAsync(Request(Allocation()) with { CustomerId = 8 }));
        await Assert.ThrowsAsync<ValidationException>(() => fixture.Engine.PostAsync(Request(Allocation()) with { BusinessKey = "RANDOM" }));
        Assert.Equal(1, await fixture.Db.WalletTransactions.CountAsync());
    }

    [Fact]
    public async Task CompletedOperationComponentAndLedgerCannotBeEditedOrDeleted()
    {
        await using var fixture = await WalletEngineFixture.CreateAsync();
        await fixture.SeedTypesAsync();
        await fixture.Engine.PostAsync(Request(Allocation()));
        fixture.Db.ChangeTracker.Clear();
        var transaction = await fixture.Db.WalletTransactions.SingleAsync();
        fixture.Db.Entry(transaction).Property(x => x.Amount).CurrentValue = 999m;
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Db.SaveChangesAsync());
        fixture.Db.ChangeTracker.Clear();
        fixture.Db.WalletOperationComponents.Remove(await fixture.Db.WalletOperationComponents.SingleAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Db.SaveChangesAsync());
        fixture.Db.ChangeTracker.Clear();
        var operation = await fixture.Db.WalletOperations.SingleAsync();
        fixture.Db.Entry(operation).Property(x => x.CalculationVersion).CurrentValue = "CHANGED";
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Db.SaveChangesAsync());
    }

    [Theory]
    [InlineData("1.005", "1.01")]
    [InlineData("-1.005", "-1.01")]
    [InlineData("1.004", "1.00")]
    public void RoundingUsesDecimalAwayFromZero(string value, string expected)
    {
        var culture = System.Globalization.CultureInfo.InvariantCulture;
        Assert.Equal(decimal.Parse(expected, culture), WalletRoundingPolicy.RoundAmount(decimal.Parse(value, culture)));
    }
}

internal sealed class WalletEngineFixture : IAsyncDisposable
{
    private readonly ServiceProvider _provider;
    private readonly AsyncServiceScope _scope;
    private readonly SqliteConnection? _sqlite;
    private readonly string? _ownedDatabase;
    public IServiceProvider Services => _scope.ServiceProvider;
    public ApplicationDbContext Db => Services.GetRequiredService<ApplicationDbContext>();
    public IWalletEngineService Engine => Services.GetRequiredService<IWalletEngineService>();
    public ICustomerWalletResolver Resolver => Services.GetRequiredService<ICustomerWalletResolver>();
    public IUnitOfWork UnitOfWork => Services.GetRequiredService<IUnitOfWork>();

    private WalletEngineFixture(ServiceProvider provider, SqliteConnection? sqlite, string? ownedDatabase)
    { _provider = provider; _scope = provider.CreateAsyncScope(); _sqlite = sqlite; _ownedDatabase = ownedDatabase; }

    public AsyncServiceScope CreateScope() => _provider.CreateAsyncScope();

    public static async Task<WalletEngineFixture> CreateAsync(bool sqlServer = false)
    {
        var database = sqlServer ? $"MartWalletFoundationTests_{Guid.NewGuid():N}" : null;
        var connectionString = sqlServer
            ? $"Server=(localdb)\\MSSQLLocalDB;Database={database};Integrated Security=true;TrustServerCertificate=true"
            : "Data Source=:memory:";
        var services = new ServiceCollection();
        services.AddPersistence(new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["ConnectionStrings:DefaultConnection"] = connectionString }).Build());
        SqliteConnection? sqlite = null;
        if (!sqlServer)
        {
            sqlite = new SqliteConnection(connectionString);
            sqlite.CreateFunction("sysutcdatetime", () => DateTime.UtcNow);
            await sqlite.OpenAsync();
            // Replace SQL Server options; repositories and UnitOfWork remain production implementations.
            foreach (var registration in services.Where(x => x.ServiceType == typeof(DbContextOptions<ApplicationDbContext>) ||
                         x.ServiceType.FullName?.Contains("IDbContextOptionsConfiguration") == true).ToArray())
                services.Remove(registration);
            services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(sqlite));
        }
        services.AddLogging();
        services.AddApplication();
        var fixture = new WalletEngineFixture(services.BuildServiceProvider(), sqlite, database);
        try
        {
            // Disposable test database only. No migrations or configured application database are used.
            await fixture.Db.Database.EnsureCreatedAsync();
            return fixture;
        }
        catch
        {
            await fixture.DisposeAsync();
            throw;
        }
    }

    public async Task SeedTypesAsync()
    {
        foreach (var (id, code) in new[] { (1, "GLOBAL"), (2, WalletTypeCodes.MartWallet) })
        {
            var type = (WalletType)Activator.CreateInstance(typeof(WalletType), nonPublic: true)!;
            // Existing test convention for read-only configuration entities; not financial production logic.
            if (!Db.Database.IsSqlServer())
                typeof(WalletType).GetProperty(nameof(WalletType.Id))!.SetValue(type, id);
            typeof(WalletType).GetProperty(nameof(WalletType.Name))!.SetValue(type, code);
            typeof(WalletType).GetProperty(nameof(WalletType.Code))!.SetValue(type, code);
            typeof(WalletType).GetProperty(nameof(WalletType.IsActive))!.SetValue(type, true);
            typeof(WalletType).GetProperty(nameof(WalletType.CreatedDate))!.SetValue(type, DateTime.UtcNow);
            Db.WalletTypes.Add(type);
        }
        await Db.SaveChangesAsync();
        Db.ChangeTracker.Clear();
    }

    public async ValueTask DisposeAsync()
    {
        if (_ownedDatabase is not null)
        {
            var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(Db.Database.GetConnectionString());
            if (builder.DataSource != "(localdb)\\MSSQLLocalDB" || builder.InitialCatalog != _ownedDatabase ||
                !_ownedDatabase.StartsWith("MartWalletFoundationTests_", StringComparison.Ordinal))
                throw new InvalidOperationException("Refusing to delete a database not owned by this test.");
            await Db.Database.EnsureDeletedAsync();
        }
        await _scope.DisposeAsync();
        await _provider.DisposeAsync();
        if (_sqlite is not null) await _sqlite.DisposeAsync();
    }
}
