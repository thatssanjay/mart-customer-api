using System.Reflection;
using Mart.Customer.Application;
using Mart.Customer.Application.Promotions;
using Mart.Customer.Application.Abstractions.Data;
using Mart.Customer.Application.Wallets.Engine;
using Mart.Customer.Domain.Common;
using Mart.Customer.Domain.Orders;
using Mart.Customer.Domain.Promotions;
using Mart.Customer.Domain.Wallets;
using Mart.Customer.Persistence;
using Mart.Customer.Persistence.MasterData;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Mart.Customer.Tests.Promotions;

public sealed class StorePromotionServiceTests
{
    [Fact]
    public async Task GetActivePromotionsReturnsOnlyCurrentFranchiseAndStorePromotionsWithinActiveDates()
    {
        await using var fixture = CreateFixture();
        var walletType = CreateWalletType(3, "Reward Wallet", "REWARD_WALLET");
        var currentStore = CreateStore(11, 7, "Current Store");
        var otherStore = CreateStore(12, 8, "Other Store");
        var now = DateTime.UtcNow;
        fixture.Db.AddRange(
            walletType,
            currentStore,
            otherStore,
            CreatePromotion(1, "CURRENT", 11, 3, StorePromotionDiscountTypes.TotalBonusPoint, 50m, null),
            CreatePromotion(2, "OTHERSTORE", 12, 3, StorePromotionDiscountTypes.TotalBonusPoint, 50m, null),
            CreatePromotion(3, "INACTIVE", 11, 3, StorePromotionDiscountTypes.TotalBonusPoint, 50m, null, isActive: false),
            CreatePromotion(4, "EXPIRED", 11, 3, StorePromotionDiscountTypes.TotalBonusPoint, 50m, null,
                startDate: now.AddDays(-2), endDate: now.AddDays(-1)),
            CreatePromotion(5, "FUTURE", 11, 3, StorePromotionDiscountTypes.TotalBonusPoint, 50m, null,
                startDate: now.AddDays(1), endDate: now.AddDays(2)));
        await fixture.Db.SaveChangesAsync();

        var results = await fixture.Service.GetActivePromotionsAsync(7, 11);
        var wrongFranchiseResults = await fixture.Service.GetActivePromotionsAsync(8, 11);

        Assert.Equal("CURRENT", Assert.Single(results).PromoCode);
        Assert.Empty(wrongFranchiseResults);
    }

    [Fact]
    public async Task SearchTodayOrdersExcludesPreviousDatesAndOtherStores()
    {
        await using var fixture = CreateFixture();
        var today = CreateOrder(101, DateTime.UtcNow.Date.AddHours(2), 7, 11, "9876543210");
        var yesterday = CreateOrder(102, DateTime.UtcNow.Date.AddDays(-1).AddHours(2), 7, 11, "9876543210");
        var otherStore = CreateOrder(103, DateTime.UtcNow.Date.AddHours(3), 7, 12, "9876543210");
        fixture.Db.CustomerOrders.AddRange(today, yesterday, otherStore);
        await fixture.Db.SaveChangesAsync();

        var results = await fixture.Service.SearchTodayOrdersAsync(7, 11, null, "9876543210");

        Assert.Equal(today.CustomerOrderId, Assert.Single(results).CustomerOrderId);
    }

    [Fact]
    public async Task AllocateCreditsConfiguredWalletAndUpdatesOrderOnce()
    {
        await using var fixture = CreateFixture();
        var order = CreateOrder(201, DateTime.UtcNow.Date.AddHours(2), 7, 11, "9876543210", 1000m);
        var walletType = CreateWalletType(3, "Reward Wallet", "REWARD_WALLET");
        var wallet = CustomerWallet.Create(order.CustomerId, 3, DateTime.UtcNow);
        var promotion = CreatePromotion(9, "PROMO123", 11, 3, StorePromotionDiscountTypes.BillPercent, 0m, 10m);
        fixture.Db.AddRange(order, walletType, wallet, promotion);
        await fixture.Db.SaveChangesAsync();

        var result = await fixture.Service.AllocateAsync(41, 7, 11, order.CustomerOrderId, "promo123", "Admin");

        Assert.Equal(100m, result.AwardedAmount);
        Assert.Equal(100m, (await fixture.Db.CustomerWallets.SingleAsync()).CurrentBalance);
        var transaction = await fixture.Db.WalletTransactions.AsNoTracking().SingleAsync();
        Assert.Equal("CREDIT", transaction.TransactionType);
        Assert.Equal(order.CustomerOrderId, transaction.ReferenceId);
        Assert.Single(await fixture.Db.WalletBalanceBuckets.AsNoTracking().ToListAsync());
        var persistedOrder = await fixture.Db.CustomerOrders.AsNoTracking().SingleAsync();
        Assert.Equal(1, persistedOrder.Status);
        Assert.Equal("Store promotion awarded", persistedOrder.Remarks);

        var duplicate = await Assert.ThrowsAsync<DomainException>(() =>
            fixture.Service.AllocateAsync(41, 7, 11, order.CustomerOrderId, "PROMO123", "Admin"));
        Assert.Contains("already", duplicate.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Single(await fixture.Db.WalletTransactions.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task AllocateRejectsAnOrderFromAnotherStoreWithoutPostingWalletHistory()
    {
        await using var fixture = CreateFixture();
        var order = CreateOrder(301, DateTime.UtcNow.Date.AddHours(2), 7, 12, "9876543210");
        fixture.Db.CustomerOrders.Add(order);
        await fixture.Db.SaveChangesAsync();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            fixture.Service.AllocateAsync(41, 7, 11, order.CustomerOrderId, "PROMO123", "Admin"));
        Assert.Empty(await fixture.Db.WalletTransactions.AsNoTracking().ToListAsync());
        Assert.Equal(0, (await fixture.Db.CustomerOrders.AsNoTracking().SingleAsync()).Status);
    }

    private static PromotionFixture CreateFixture()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Server=(local);Database=unused;Trusted_Connection=True;"
            })
            .Build();
        services.AddLogging();
        services.AddApplication();
        services.AddPersistence(configuration);
        services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
        services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseInMemoryDatabase($"PromotionTests-{Guid.NewGuid():N}"));
        services.RemoveAll<IWalletPostingGuard>();
        services.AddScoped<IWalletPostingGuard, TestWalletPostingGuard>();
        var provider = services.BuildServiceProvider();
        var scope = provider.CreateAsyncScope();
        return new PromotionFixture(
            provider,
            scope,
            scope.ServiceProvider.GetRequiredService<ApplicationDbContext>(),
            scope.ServiceProvider.GetRequiredService<IStorePromotionService>());
    }

    private static CustomerOrder CreateOrder(
        long cartId,
        DateTime orderDate,
        long franchiseId,
        long storeId,
        string mobile,
        decimal amount = 118m)
    {
        var order = CustomerOrder.Create(
            cartId,
            $"INV-PROMO-{cartId}",
            cartId + 1000,
            franchiseId,
            storeId,
            orderDate,
            1,
            amount,
            0m,
            0m,
            amount,
            null,
            0m,
            amount,
            41,
            customerNameSnapshot: "Anita Rao",
            customerMobileSnapshot: mobile);
        Set(order, nameof(CustomerOrder.CustomerOrderId), cartId);
        return order;
    }

    private static WalletType CreateWalletType(int id, string name, string code)
    {
        var walletType = (WalletType)Activator.CreateInstance(typeof(WalletType), nonPublic: true)!;
        Set(walletType, nameof(WalletType.Id), id);
        Set(walletType, nameof(WalletType.Name), name);
        Set(walletType, nameof(WalletType.Code), code);
        Set(walletType, nameof(WalletType.IsActive), true);
        return walletType;
    }

    private static MartStoreEntity CreateStore(long id, long franchiseId, string name)
    {
        var store = (MartStoreEntity)Activator.CreateInstance(typeof(MartStoreEntity), nonPublic: true)!;
        Set(store, nameof(MartStoreEntity.StoreId), id);
        Set(store, nameof(MartStoreEntity.FranchiseId), franchiseId);
        Set(store, nameof(MartStoreEntity.StoreName), name);
        Set(store, nameof(MartStoreEntity.IsActive), true);
        return store;
    }

    private static StorePromotion CreatePromotion(
        int id,
        string code,
        long storeId,
        int walletTypeId,
        string discountType,
        decimal bonusPoint,
        decimal? percent,
        bool isActive = true,
        DateTime? startDate = null,
        DateTime? endDate = null)
    {
        var promotion = (StorePromotion)Activator.CreateInstance(typeof(StorePromotion), nonPublic: true)!;
        Set(promotion, nameof(StorePromotion.Id), id);
        Set(promotion, nameof(StorePromotion.PromoCode), code);
        Set(promotion, nameof(StorePromotion.StoreId), storeId);
        Set(promotion, nameof(StorePromotion.WalletTypeId), walletTypeId);
        Set(promotion, nameof(StorePromotion.DiscountType), discountType);
        Set(promotion, nameof(StorePromotion.BonusPoint), bonusPoint);
        Set(promotion, nameof(StorePromotion.BillPercentDiscount), percent);
        Set(promotion, nameof(StorePromotion.StartDate), startDate ?? DateTime.UtcNow.AddDays(-1));
        Set(promotion, nameof(StorePromotion.EndDate), endDate ?? DateTime.UtcNow.AddDays(1));
        Set(promotion, nameof(StorePromotion.IsActive), isActive);
        return promotion;
    }

    private static void Set<T>(T target, string propertyName, object? value) where T : class =>
        typeof(T).GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public)!.SetValue(target, value);

    private sealed class PromotionFixture(
        ServiceProvider provider,
        AsyncServiceScope scope,
        ApplicationDbContext db,
        IStorePromotionService service) : IAsyncDisposable
    {
        public ApplicationDbContext Db { get; } = db;
        public IStorePromotionService Service { get; } = service;

        public async ValueTask DisposeAsync()
        {
            await scope.DisposeAsync();
            await provider.DisposeAsync();
        }
    }

    private sealed class TestWalletPostingGuard : IWalletPostingGuard
    {
        public void EnsureCleanEntry() { }
        public void EnsureTransaction() { }
        public void EnsureTracked(object entity) { }
        public bool IsRetryableConflict(Exception exception) => false;
    }
}
