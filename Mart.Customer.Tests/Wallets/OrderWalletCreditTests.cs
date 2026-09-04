using Mart.Customer.Application.Wallets.Engine;
using Mart.Customer.Domain.Carts;
using Mart.Customer.Domain.Cashback;
using Mart.Customer.Domain.Common;
using Mart.Customer.Domain.Orders;
using Mart.Customer.Domain.Subscriptions;
using Mart.Customer.Domain.Wallets;
using Mart.Customer.Persistence.MasterData;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Mart.Customer.Tests.Wallets;

public sealed class OrderWalletCreditTests
{
    [Theory]
    [InlineData("Wallet")]
    [InlineData("WALLET")]
    [InlineData("wallet")]
    [InlineData(" Wallet ")]
    [InlineData("APP")]
    public async Task AppPaymentStoredAsWalletReceivesAllocationOnce(string mode)
    {
        await using var f = await WalletEngineFixture.CreateAsync();
        var (order, access) = await SeedAsync(f, mode: mode);
        var result = await f.Engine.CreditPaidOrderAsync(order.CustomerOrderId, access);
        Assert.Equal(new[] { 900m, 100m }, result.Wallets.Select(x => x.CreditedAmount));
        Assert.True((await f.Engine.CreditPaidOrderAsync(order.CustomerOrderId, access)).IsIdempotentReplay);
        Assert.Equal(2, await f.Db.WalletTransactions.CountAsync());
    }

    [Fact]
    public async Task CustomerCanCreditOwnOrderAndReplayButCannotCreditAnotherCustomersOrder()
    {
        await using var f = await WalletEngineFixture.CreateAsync();
        var (order, _) = await SeedAsync(f);
        var stranger = new OrderWalletAccess(order.CustomerId + 1, 0, 0, order.CustomerId + 1);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            f.Engine.CreditPaidOrderAsync(order.CustomerOrderId, stranger));
        Assert.Empty(await f.Db.WalletOperations.ToListAsync());
        var owner = new OrderWalletAccess(order.CustomerId, 0, 0, order.CustomerId);
        var credited = await f.Engine.CreditPaidOrderAsync(order.CustomerOrderId, owner);
        Assert.False(credited.IsIdempotentReplay);
        Assert.Equal(order.MartStoreId, credited.StoreId);
        Assert.True((await f.Engine.CreditPaidOrderAsync(order.CustomerOrderId, owner)).IsIdempotentReplay);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            f.Engine.CreditPaidOrderAsync(order.CustomerOrderId, stranger));
        Assert.Equal(2, await f.Db.WalletTransactions.CountAsync());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("0")]
    [InlineData("0.25")]
    [InlineData("10")]
    public async Task NinetyTenAllocationIgnoresConversionAndUsesCanonicalScopes(string? conversion)
    {
        await using var f = await WalletEngineFixture.CreateAsync();
        var (order, access) = await SeedAsync(f, conversion);
        var result = await f.Engine.CreditPaidOrderAsync(order.CustomerOrderId, access);
        Assert.Equal(1000m, result.PaidAmount);
        Assert.False(result.IsIdempotentReplay);
        Assert.Equal(new[] { 900m, 100m }, result.Wallets.Select(x => x.CreditedAmount));
        Assert.Null(result.Wallets[0].StoreId);
        Assert.Equal(order.MartStoreId, result.Wallets[1].StoreId);
        Assert.Equal(2, await f.Db.WalletTransactions.CountAsync());
        Assert.Equal(2, await f.Db.WalletBalanceBuckets.CountAsync());
        Assert.All(await f.Db.CustomerWallets.AsNoTracking().ToListAsync(), w => Assert.Equal(w.CurrentBalance, w.TotalCredit));
        Assert.All(await f.Db.WalletTransactions.ToListAsync(), t => Assert.Equal(order.CustomerOrderId, t.ReferenceId));
    }

    [Fact]
    public async Task ReplayReturnsOriginalBalancesWithoutRecalculatingConfiguration()
    {
        await using var f = await WalletEngineFixture.CreateAsync();
        var (order, access) = await SeedAsync(f);
        var first = await f.Engine.CreditPaidOrderAsync(order.CustomerOrderId, access);
        f.Db.ChangeTracker.Clear();
        var rule = await f.Db.CashbackSettingWallets.FirstAsync();
        Set(rule, nameof(CashbackSettingWallet.PointPercentage), 1m);
        await f.Db.SaveChangesAsync();
        var replay = await f.Engine.CreditPaidOrderAsync(order.CustomerOrderId, access);
        Assert.True(replay.IsIdempotentReplay);
        Assert.Equal(first.Wallets, replay.Wallets);
        Assert.Equal(1, await f.Db.WalletOperations.CountAsync());
        Assert.Equal(2, await f.Db.WalletTransactions.CountAsync());
    }

    [Theory]
    [InlineData("Pending", "APP", "1000")]
    [InlineData("Failed", "APP", "1000")]
    [InlineData("Paid", "APP", "500")]
    [InlineData("Paid", "Cash", "1000")]
    [InlineData("Pending", "Wallet", "1000")]
    [InlineData("Paid", "Wallet", "500")]
    [InlineData("Paid", "UPI", "1000")]
    [InlineData("Paid", "Card", "1000")]
    public async Task RejectsUnpaidAndIneligibleOrders(string status, string mode, string paid)
    {
        await using var f = await WalletEngineFixture.CreateAsync();
        var (order, access) = await SeedAsync(f, mode: mode, amount: decimal.Parse(paid));
        var tracked = await f.Db.CustomerOrders.SingleAsync();
        Set(tracked, nameof(CustomerOrder.OrderStatus), status);
        await f.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<DomainException>(() => f.Engine.CreditPaidOrderAsync(order.CustomerOrderId, access));
        Assert.Empty(await f.Db.WalletOperations.ToListAsync());
    }

    [Theory]
    [InlineData("missing-order")]
    [InlineData("missing-config")]
    [InlineData("inactive-customer")]
    [InlineData("inactive-store")]
    [InlineData("legacy-reward")]
    public async Task RejectsInvalidSourcesWithoutPosting(string reason)
    {
        await using var f = await WalletEngineFixture.CreateAsync();
        var (order, access) = await SeedAsync(f);
        if (reason == "missing-config") Set(await f.Db.CashbackConfigurations.SingleAsync(), "IsActive", false);
        if (reason == "inactive-customer") Set(await f.Db.Customers.SingleAsync(), "IsActive", false);
        if (reason == "inactive-store") Set(await f.Db.MartStores.SingleAsync(), "IsActive", false);
        if (reason == "legacy-reward") (await f.Db.CustomerOrders.SingleAsync()).SetRewardEarned(1m);
        await f.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<DomainException>(() => f.Engine.CreditPaidOrderAsync(
            reason == "missing-order" ? long.MaxValue : order.CustomerOrderId, access));
        Assert.Empty(await f.Db.WalletTransactions.ToListAsync());
    }

    [Fact]
    public async Task RejectsDifferentStoreBeforePosting()
    {
        await using var f = await WalletEngineFixture.CreateAsync();
        var (order, access) = await SeedAsync(f);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Engine.CreditPaidOrderAsync(
            order.CustomerOrderId, access with { StoreId = access.StoreId + 1 }));
    }

    [Fact]
    public async Task SubscriptionBonusUsesExistingPercentageAndSeparateComponent()
    {
        await using var f = await WalletEngineFixture.CreateAsync();
        var (order, access) = await SeedAsync(f);
        var subscription = New<CustomerSubscription>();
        var plan = New<SubscriptionPlan>();
        Set(plan, "PlanName", "Test plan");
        Set(plan, "DurationType", "DAY");
        f.Db.SubscriptionPlans.Add(plan);
        await f.Db.SaveChangesAsync();
        Set(subscription, "SubscriptionPlanId", plan.SubscriptionId);
        Set(subscription, "CustomerId", order.CustomerId);
        Set(subscription, "Status", "ACTIVE");
        Set(subscription, "StartDate", order.OrderDate.Date.AddDays(-1));
        Set(subscription, "ExpiryDate", order.OrderDate.AddDays(30));
        Set(subscription, "ExtraPointPercentage", 5m);
        Set(subscription, "WalletTypeId", 1);
        f.Db.CustomerSubscriptions.Add(subscription);
        await f.Db.SaveChangesAsync();
        var result = await f.Engine.CreditPaidOrderAsync(order.CustomerOrderId, access);
        Assert.Equal(1050m, result.Wallets.Sum(x => x.CreditedAmount));
        var bonus = Assert.Single(await f.Db.WalletOperationComponents.ToListAsync(), x => x.ComponentCode == WalletComponentCodes.SubscriptionBonus);
        Assert.Equal(50m, (await f.Db.WalletTransactions.SingleAsync(x => x.WalletTransactionId == bonus.WalletTransactionId)).Amount);
        Assert.Equal(3, await f.Db.WalletBalanceBuckets.CountAsync());
        Assert.Equal(950m, (await f.Db.CustomerWallets.AsNoTracking().SingleAsync(x => x.WalletTypeId == 1)).CurrentBalance);
    }

    [Fact]
    public async Task FailureOnSecondWalletRollsBackEveryWrite()
    {
        await using var f = await WalletEngineFixture.CreateAsync();
        var (order, access) = await SeedAsync(f);
        var inactive = CustomerWallet.Create(order.CustomerId, 2, order.OrderDate, order.MartStoreId);
        inactive.UpdateStatus(false, order.OrderDate);
        f.Db.CustomerWallets.Add(inactive);
        await f.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<DomainException>(() => f.Engine.CreditPaidOrderAsync(order.CustomerOrderId, access));
        Assert.Empty(await f.Db.WalletOperations.ToListAsync());
        Assert.Empty(await f.Db.WalletTransactions.ToListAsync());
        Assert.Empty(await f.Db.WalletBalanceBuckets.ToListAsync());
        Assert.Empty(await f.Db.WalletOperationComponents.ToListAsync());
        Assert.Equal(0m, Assert.Single(await f.Db.CustomerWallets.AsNoTracking().ToListAsync()).CurrentBalance);
    }

    [Fact]
    public async Task CapIsSharedAcrossBaseWallets()
    {
        await using var f = await WalletEngineFixture.CreateAsync();
        var (order, access) = await SeedAsync(f);
        Set(await f.Db.CashbackConfigurations.SingleAsync(), "MaximumCashbackPerOrder", 100m);
        await f.Db.SaveChangesAsync();
        var result = await f.Engine.CreditPaidOrderAsync(order.CustomerOrderId, access);
        Assert.Equal(new[] { 90m, 10m }, result.Wallets.Select(x => x.CreditedAmount));
    }

    [LocalWalletSqlServerFact]
    public async Task ConcurrentOrderRequestsCreditOnceOnSqlServer()
    {
        await using var f = await WalletEngineFixture.CreateAsync(sqlServer: true);
        var (order, access) = await SeedAsync(f);
        async Task<OrderWalletCreditResult> Credit()
        {
            await using var scope = f.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<IWalletEngineService>()
                .CreditPaidOrderAsync(order.CustomerOrderId, access);
        }
        var results = await Task.WhenAll(Credit(), Credit());
        Assert.Single(results, x => x.IsIdempotentReplay);
        Assert.Equal(1, await f.Db.WalletOperations.CountAsync());
        Assert.Equal(2, await f.Db.CustomerWallets.CountAsync());
        Assert.Equal(2, await f.Db.WalletTransactions.CountAsync());
        Assert.Equal(2, await f.Db.WalletBalanceBuckets.CountAsync());
        // A different business key cannot bypass the order-level database constraint.
        var duplicate = WalletOperation.CreateSaleReward("DUPLICATE-ORDER", WalletOperation.SaleBusinessKey(order.CustomerOrderId),
            order.CustomerId, order.MartStoreId, order.CustomerOrderId, null, order.OrderDate, "V1", "test", DateTime.UtcNow);
        Set(duplicate, "BusinessKey", "DIFFERENT-KEY");
        f.Db.WalletOperations.Add(duplicate);
        var failure = await Assert.ThrowsAsync<DbUpdateException>(() => f.Db.SaveChangesAsync());
        Assert.Contains("UQ_WalletOperation_Order", failure.GetBaseException().Message);
        f.Db.ChangeTracker.Clear();
    }

    [Fact]
    public async Task MinimumPurchaseRecordsNoRewardAndCannotBeReprocessed()
    {
        await using var f = await WalletEngineFixture.CreateAsync();
        var (order, access) = await SeedAsync(f);
        Set(await f.Db.CashbackConfigurations.SingleAsync(), "MinimumPurchaseAmount", 2000m);
        await f.Db.SaveChangesAsync();
        Assert.Empty((await f.Engine.CreditPaidOrderAsync(order.CustomerOrderId, access)).Wallets);
        Assert.True((await f.Engine.CreditPaidOrderAsync(order.CustomerOrderId, access)).IsIdempotentReplay);
        Assert.Empty(await f.Db.WalletTransactions.ToListAsync());
        Assert.Equal(1, await f.Db.WalletOperations.CountAsync());
    }

    [Fact]
    public async Task ExpiryIsPreservedOnCreditBucket()
    {
        await using var f = await WalletEngineFixture.CreateAsync();
        var (order, access) = await SeedAsync(f);
        var rule = await f.Db.CashbackSettingWallets.FirstAsync(x => x.WalletTypeId == 2);
        var expiry = DateTime.SpecifyKind(order.OrderDate.Date.AddDays(30), DateTimeKind.Utc);
        Set(rule, "IsNoExpiry", false);
        Set(rule, "StartDate", order.OrderDate.AddDays(-1));
        Set(rule, "EndDate", expiry);
        await f.Db.SaveChangesAsync();
        await f.Engine.CreditPaidOrderAsync(order.CustomerOrderId, access);
        var buckets = await f.Db.WalletBalanceBuckets.ToListAsync();
        Assert.Single(buckets, b => b.ExpiryDate == expiry);
        Assert.Single(buckets, b => b.ExpiryDate == null);
    }

    [Fact]
    public async Task MixedAppPaymentIsIneligibleEvenWhenFullyPaid()
    {
        await using var f = await WalletEngineFixture.CreateAsync();
        var (order, access) = await SeedAsync(f, amount: 500m);
        (await f.Db.CustomerOrders.SingleAsync()).AddPayment("Cash", 500m, null);
        await f.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<DomainException>(() => f.Engine.CreditPaidOrderAsync(order.CustomerOrderId, access));
        Assert.Empty(await f.Db.WalletOperations.ToListAsync());
    }

    private static async Task<(CustomerOrder, OrderWalletAccess)> SeedAsync(WalletEngineFixture f,
        string? conversion = null, string mode = "Wallet", decimal amount = 1000m)
    {
        await f.SeedTypesAsync();
        var customer = New<Mart.Customer.Domain.Customers.Customer>();
        Set(customer, "MobileNumber", "9000000000");
        Set(customer, "IsActive", true);
        Set(customer, "CreatedOn", DateTime.UtcNow);
        var store = New<MartStoreEntity>();
        Set(store, "StoreName", "Test store");
        Set(store, "FranchiseId", 7L);
        Set(store, "IsActive", true);
        f.Db.AddRange(customer, store);
        await f.Db.SaveChangesAsync();
        var cart = CustomerCart.Create(customer.CustomerId, 7, store.StoreId, Guid.NewGuid().ToString("N"), 41);
        f.Db.CustomerCarts.Add(cart);
        await f.Db.SaveChangesAsync();
        var order = CustomerOrder.Create(cart.CustomerCartId, "INV-TEST", customer.CustomerId, 7, store.StoreId,
            DateTime.UtcNow.AddMinutes(-1), 0, 1000m, 0, 0, 1000m, null, 0, 1000m, 41);
        order.AddPayment(mode, amount, "SETTLED-TEST");
        f.Db.CustomerOrders.Add(order);
        var config = New<CashbackConfiguration>();
        Set(config, "StoreId", store.StoreId);
        Set(config, "IsActive", true);
        f.Db.CashbackConfigurations.Add(config);
        await f.Db.SaveChangesAsync();
        foreach (var (typeId, percentage) in new[] { (1, 90m), (2, 10m) })
        {
            var rule = New<CashbackSettingWallet>();
            Set(rule, "CashbackSettingId", config.CashbackSettingId);
            Set(rule, "WalletTypeId", typeId);
            Set(rule, "PointPercentage", percentage);
            Set(rule, "ConversionRate", conversion is null ? null : decimal.Parse(conversion, System.Globalization.CultureInfo.InvariantCulture));
            Set(rule, "IsNoExpiry", true);
            Set(rule, "IsActive", true);
            f.Db.CashbackSettingWallets.Add(rule);
        }
        await f.Db.SaveChangesAsync();
        f.Db.ChangeTracker.Clear();
        return (order, new OrderWalletAccess(41, 7, store.StoreId));
    }

    private static T New<T>() => (T)Activator.CreateInstance(typeof(T), nonPublic: true)!;
    private static void Set(object target, string property, object? value) => target.GetType().GetProperty(property)!.SetValue(target, value);
}
