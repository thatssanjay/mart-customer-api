using Mart.Customer.Application.Wallets.Engine;
using Mart.Customer.Domain.Common;
using Mart.Customer.Domain.Wallets;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static Mart.Customer.Tests.Wallets.WalletEngineFoundationTests;

namespace Mart.Customer.Tests.Wallets;

public sealed class WalletEngineSqlServerTests
{
    [LocalWalletSqlServerFact]
    public async Task FoundationScriptRestoresMissingTablesAndCanBeRunAgainWithoutLosingCredits()
    {
        await using var fixture = await WalletEngineFixture.CreateAsync(sqlServer: true);
        await fixture.SeedTypesAsync();
        // Only the fixture-owned disposable database is modified; no application connection is used.
        await fixture.Db.Database.ExecuteSqlRawAsync("DROP TABLE [Wallet].[WalletOperationComponent]; DROP TABLE [Wallet].[WalletOperation];");
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        string? scriptPath = null;
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "Mart.Customer.Persistence", "Scripts", "WalletEngineFoundation.sql");
            if (File.Exists(candidate)) { scriptPath = candidate; break; }
            directory = directory.Parent;
        }
        Assert.NotNull(scriptPath);
        var script = await File.ReadAllTextAsync(scriptPath);
        await fixture.Db.Database.ExecuteSqlRawAsync(script);
        var request = Request(Allocation(), Allocation(2, store: 11));
        var first = await fixture.Engine.PostAsync(request);
        await fixture.Db.Database.ExecuteSqlRawAsync(script);
        var replay = await fixture.Engine.PostAsync(request);
        Assert.True(replay.IsIdempotentReplay);
        Assert.Equal(first.WalletOperationId, replay.WalletOperationId);
        Assert.Equal(2, await fixture.Db.WalletTransactions.CountAsync());
        Assert.Equal(2, await fixture.Db.WalletBalanceBuckets.CountAsync());
        Assert.Equal(2, await fixture.Db.WalletOperationComponents.CountAsync());
    }

    [LocalWalletSqlServerFact]
    public async Task ConcurrentSameOperationAndDifferentOrdersAreSafeOnSqlServer()
    {
        await using var fixture = await WalletEngineFixture.CreateAsync(sqlServer: true);
        await fixture.SeedTypesAsync();
        async Task<WalletEngineResult> Post(WalletPostingRequest request)
        {
            await using var scope = fixture.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<IWalletEngineService>().PostAsync(request);
        }
        var request = Request(Allocation(), Allocation(2, store: 11));
        var results = await Task.WhenAll(Post(request), Post(request));
        Assert.Single(results, x => x.IsIdempotentReplay);
        Assert.Equal(results[0].WalletOperationId, results[1].WalletOperationId);
        Assert.Equal(1, await fixture.Db.WalletOperations.CountAsync());
        Assert.Equal(2, await fixture.Db.CustomerWallets.CountAsync());
        Assert.Equal(2, await fixture.Db.WalletTransactions.CountAsync());
        Assert.Equal(2, await fixture.Db.WalletBalanceBuckets.CountAsync());

        // Different operations race to create the same previously absent customer wallets.
        await Task.WhenAll(Post(request with { CustomerId = 8, CustomerOrderId = 101, BusinessKey = WalletOperation.SaleBusinessKey(101) }),
            Post(request with { CustomerId = 8, CustomerOrderId = 102, BusinessKey = WalletOperation.SaleBusinessKey(102) }));
        var wallets = await fixture.Db.CustomerWallets.AsNoTracking().Where(x => x.CustomerId == 8).ToListAsync();
        Assert.Equal(2, wallets.Count);
        Assert.All(wallets, x => { Assert.Equal(20m, x.CurrentBalance); Assert.Equal(20m, x.TotalCredit); });
        Assert.Equal(6, await fixture.Db.WalletTransactions.CountAsync());
    }

    [LocalWalletSqlServerFact]
    public async Task SqlServerRollsBackFirstPostingWhenSecondWalletFails()
    {
        await using var fixture = await WalletEngineFixture.CreateAsync(sqlServer: true);
        await fixture.SeedTypesAsync();
        var second = CustomerWallet.Create(7, 2, EffectiveAt, 11);
        second.UpdateStatus(false, EffectiveAt);
        fixture.Db.CustomerWallets.Add(second);
        await fixture.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<DomainException>(() => fixture.Engine.PostAsync(Request(Allocation(), Allocation(2))));
        Assert.Empty(await fixture.Db.WalletOperations.ToListAsync());
        Assert.Empty(await fixture.Db.WalletOperationComponents.ToListAsync());
        Assert.Empty(await fixture.Db.WalletTransactions.ToListAsync());
        Assert.Empty(await fixture.Db.WalletBalanceBuckets.ToListAsync());
        var wallet = Assert.Single(await fixture.Db.CustomerWallets.AsNoTracking().ToListAsync());
        Assert.Equal(0m, wallet.CurrentBalance);
        Assert.Equal(0m, wallet.TotalCredit);
    }

    [LocalWalletSqlServerFact]
    public async Task SqlServerEnforcesOperationComponentBucketAndGlobalWalletUniqueness()
    {
        await using var fixture = await WalletEngineFixture.CreateAsync(sqlServer: true);
        await fixture.SeedTypesAsync();
        WalletOperation NewOperation(string number) => WalletOperation.CreateSaleReward(number,
            WalletOperation.SaleBusinessKey(100), 7, 11, 100, 200, EffectiveAt, "V1", "tests", DateTime.UtcNow);

        var duplicateOperation = await Assert.ThrowsAsync<DbUpdateException>(() => fixture.UnitOfWork.ExecuteInTransactionAsync(async token =>
        {
            fixture.Db.WalletOperations.Add(NewOperation("FIRST"));
            await fixture.Db.SaveChangesAsync(token);
            fixture.Db.WalletOperations.Add(NewOperation("SECOND"));
            await fixture.Db.SaveChangesAsync(token);
            return true;
        }));
        Assert.Contains("UQ_WalletOperation_Identity", duplicateOperation.GetBaseException().Message);
        fixture.Db.ChangeTracker.Clear();
        Assert.Empty(await fixture.Db.WalletOperations.ToListAsync());

        var duplicateComponent = await Assert.ThrowsAsync<DbUpdateException>(() => fixture.UnitOfWork.ExecuteInTransactionAsync(async token =>
        {
            var operation = NewOperation("COMPONENTS");
            fixture.Db.WalletOperations.Add(operation);
            await fixture.Db.SaveChangesAsync(token);
            var wallet = await fixture.Resolver.ResolveAsync(7, 1, null, token);
            var ledger = fixture.Services.GetRequiredService<IWalletLedgerService>();
            await ledger.PostCreditAsync(operation, wallet, Allocation(), token);
            await fixture.Db.SaveChangesAsync(token);
            await ledger.PostCreditAsync(operation, wallet, Allocation(), token);
            await fixture.Db.SaveChangesAsync(token);
            return true;
        }));
        Assert.Contains("UQ_WalletOperationComponent_Identity", duplicateComponent.GetBaseException().Message);
        fixture.Db.ChangeTracker.Clear();
        Assert.Empty(await fixture.Db.WalletTransactions.ToListAsync());
        Assert.Empty(await fixture.Db.CustomerWallets.ToListAsync());

        await fixture.Engine.PostAsync(Request(Allocation()));
        fixture.Db.ChangeTracker.Clear();
        var transaction = await fixture.Db.WalletTransactions.SingleAsync();
        fixture.Db.WalletBalanceBuckets.Add(WalletBalanceBucket.Create(transaction.CustomerWalletId,
            transaction, transaction.Amount, null, EffectiveAt));
        var duplicateBucket = await Assert.ThrowsAsync<DbUpdateException>(() => fixture.Db.SaveChangesAsync());
        Assert.Contains("IX_WalletBalanceBucket_SourceTransactionId", duplicateBucket.GetBaseException().Message);
        fixture.Db.ChangeTracker.Clear();
        fixture.Db.CustomerWallets.Add(CustomerWallet.Create(7, 1, EffectiveAt));
        var duplicateWallet = await Assert.ThrowsAsync<DbUpdateException>(() => fixture.Db.SaveChangesAsync());
        Assert.Contains("UQ_CustomerWallet", duplicateWallet.GetBaseException().Message);
        fixture.Db.ChangeTracker.Clear();
        var bucket = await fixture.Db.WalletBalanceBuckets.SingleAsync();
        fixture.Db.Entry(bucket).Property(x => x.AvailableAmount).CurrentValue = -1m;
        var invalidBucket = await Assert.ThrowsAsync<DbUpdateException>(() => fixture.Db.SaveChangesAsync());
        Assert.Contains("CK_WalletBalanceBucket_AvailableAmount", invalidBucket.GetBaseException().Message);
    }
}

public sealed class LocalWalletSqlServerFactAttribute : FactAttribute
{
    public LocalWalletSqlServerFactAttribute()
    {
        if (!OperatingSystem.IsWindows() || Environment.GetEnvironmentVariable("MART_WALLET_LOCALDB_TESTS") != "1")
            Skip = "Opt in with MART_WALLET_LOCALDB_TESTS=1; uses disposable databases on local MSSQLLocalDB only.";
    }
}
