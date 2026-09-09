using FluentValidation.TestHelper;
using Mart.Customer.Api.Auth;
using Mart.Customer.Api.Controllers.V1;
using Mart.Customer.Application;
using Mart.Customer.Application.Abstractions.Data;
using Mart.Customer.Application.Wallets.Commands.ConvertRewardToStoreWallet;
using Mart.Customer.Domain.Cashback;
using Mart.Customer.Domain.Common;
using Mart.Customer.Domain.Wallets;
using Mart.Customer.Persistence;
using Mart.Customer.Persistence.MasterData;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Mart.Customer.Tests.Wallets;

public sealed class RewardStoreWalletConversionTests
{
    [Fact]
    public void Endpoint_UsesCustomerTokenAndAcceptsNoCustomerId()
    {
        var method = typeof(CustomersController)
            .GetMethod(nameof(CustomersController.ConvertRewardToStoreWallet))!;
        var route = Assert.Single(method.GetCustomAttributes(typeof(HttpPostAttribute), false)
            .Cast<HttpPostAttribute>());
        var authorization = Assert.Single(method
            .GetCustomAttributes(typeof(AuthorizeAttribute), false)
            .Cast<AuthorizeAttribute>());

        Assert.Equal("store-wallet-conversion", route.Template);
        Assert.Equal(MartAuthorizationPolicies.MobileCustomer, authorization.Policy);
        Assert.Contains(method.GetParameters(), parameter =>
            parameter.ParameterType == typeof(IMartUserContext));
        Assert.DoesNotContain(method.GetParameters(), parameter =>
            string.Equals(parameter.Name, "customerId", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Conversion_DebitsRewardAndCreditsOnlySelectedStoreWithLinkedHistory()
    {
        await using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await SeedAsync(db, rewardBalance: 100m, conversionRate: 5m, includeOtherStoreWallet: true);

        var result = await scope.ServiceProvider.GetRequiredService<ISender>().Send(
            Command(rewardPoints: 40m, requestId: "CONVERT-001"));

        Assert.Equal(40m, result.RewardPointsDeducted);
        Assert.Equal(2m, result.ConvertedAmount);
        Assert.Equal(60m, result.RewardBalance);
        Assert.Equal(2m, result.StoreWalletBalance);
        Assert.False(result.IsIdempotentReplay);

        var rewardWallet = await db.CustomerWallets.SingleAsync(wallet =>
            wallet.CustomerId == 1001 && wallet.WalletTypeId == 11);
        var selectedStoreWallet = await db.CustomerWallets.SingleAsync(wallet =>
            wallet.CustomerId == 1001 && wallet.WalletTypeId == 12 && wallet.StoreId == 501);
        var otherStoreWallet = await db.CustomerWallets.SingleAsync(wallet =>
            wallet.CustomerId == 1001 && wallet.WalletTypeId == 12 && wallet.StoreId == 502);
        Assert.Equal(60m, rewardWallet.CurrentBalance);
        Assert.Equal(2m, selectedStoreWallet.CurrentBalance);
        Assert.Equal(10m, otherStoreWallet.CurrentBalance);

        var conversions = await db.WalletTransactions
            .Where(transaction => transaction.ReferenceType == "REWARD_CONVERSION")
            .OrderBy(transaction => transaction.WalletTransactionId)
            .ToListAsync();
        Assert.Equal(2, conversions.Count);
        Assert.Equal(new[] { "REDEMPTION", "CREDIT" },
            conversions.Select(transaction => transaction.TransactionType));
        Assert.Single(conversions.Select(transaction => transaction.ReferenceId).Distinct());
        Assert.All(conversions, transaction =>
            Assert.Equal("Reward conversion CONVERT-001", transaction.Remarks));

        var rewardBucket = await db.WalletBalanceBuckets.SingleAsync(bucket =>
            bucket.CustomerWalletId == rewardWallet.CustomerWalletId);
        var storeBucket = await db.WalletBalanceBuckets.SingleAsync(bucket =>
            bucket.CustomerWalletId == selectedStoreWallet.CustomerWalletId);
        Assert.Equal(60m, rewardBucket.AvailableAmount);
        Assert.Equal(2m, storeBucket.AvailableAmount);
        Assert.Equal(result.StoreWalletTransactionId, storeBucket.SourceTransactionId);
    }

    [Fact]
    public async Task RepeatedRequest_ReturnsOriginalResultWithoutProcessingTwice()
    {
        await using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await SeedAsync(db, rewardBalance: 100m, conversionRate: 5m);
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var command = Command(rewardPoints: 20m, requestId: "CONVERT-REPLAY");

        var first = await sender.Send(command);
        var replay = await sender.Send(command);

        Assert.False(first.IsIdempotentReplay);
        Assert.True(replay.IsIdempotentReplay);
        Assert.Equal(first.RewardTransactionId, replay.RewardTransactionId);
        Assert.Equal(first.StoreWalletTransactionId, replay.StoreWalletTransactionId);
        Assert.Equal(80m, (await db.CustomerWallets.SingleAsync(wallet =>
            wallet.WalletTypeId == 11)).CurrentBalance);
        Assert.Equal(1m, (await db.CustomerWallets.SingleAsync(wallet =>
            wallet.WalletTypeId == 12)).CurrentBalance);
        Assert.Equal(2, await db.WalletTransactions.CountAsync(transaction =>
            transaction.ReferenceType == "REWARD_CONVERSION"));
    }

    [Fact]
    public async Task InsufficientReward_DoesNotCreateOrCreditAStoreWallet()
    {
        await using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await SeedAsync(db, rewardBalance: 10m, conversionRate: 5m);

        var exception = await Assert.ThrowsAsync<DomainException>(() =>
            scope.ServiceProvider.GetRequiredService<ISender>().Send(
                Command(rewardPoints: 11m, requestId: "CONVERT-TOO-MUCH")));

        Assert.Equal("Insufficient REWARD balance.", exception.Message);
        Assert.Single(await db.CustomerWallets.ToListAsync());
        Assert.Empty(await db.WalletTransactions
            .Where(transaction => transaction.ReferenceType == "REWARD_CONVERSION")
            .ToListAsync());
    }

    [Fact]
    public async Task Conversion_UsesCurrentRewardBalanceWhenLegacyBucketIsMissing()
    {
        await using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await SeedAsync(db, rewardBalance: 25m, conversionRate: 5m, includeRewardBucket: false);

        var result = await scope.ServiceProvider.GetRequiredService<ISender>().Send(
            Command(rewardPoints: 10m, requestId: "CONVERT-LEGACY"));

        Assert.Equal(15m, result.RewardBalance);
        Assert.Equal(0.5m, result.ConvertedAmount);
        Assert.Equal(2, await db.WalletTransactions.CountAsync(transaction =>
            transaction.ReferenceType == "REWARD_CONVERSION"));
    }

    [Fact]
    public void Validator_RejectsInvalidAmountStoreAndRequestId()
    {
        var validator = new ConvertRewardToStoreWalletCommandValidator();
        var result = validator.TestValidate(
            Command(rewardPoints: 0m, requestId: "bad request") with { StoreId = 0 });

        result.ShouldHaveValidationErrorFor(command => command.StoreId);
        result.ShouldHaveValidationErrorFor(command => command.RewardPoints);
        result.ShouldHaveValidationErrorFor(command => command.RequestId);
    }

    private static ConvertRewardToStoreWalletCommand Command(decimal rewardPoints, string requestId) =>
        new(1001, 501, rewardPoints, requestId, "customer-1001");

    private static AsyncServiceScope CreateScope()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        Register<ICashbackConfigurationRepository>(services, "CashbackConfigurationRepository");
        Register<ICustomerWalletRepository>(services, "CustomerWalletRepository");
        Register<IWalletTypeRepository>(services, "WalletTypeRepository");
        Register<IWalletTransactionRepository>(services, "WalletTransactionRepository");
        Register<IWalletBalanceBucketRepository>(services, "WalletBalanceBucketRepository");
        Register<IUnitOfWork>(services, "UnitOfWork");
        return services.BuildServiceProvider().CreateAsyncScope();
    }

    private static void Register<TService>(IServiceCollection services, string implementationName)
        where TService : class => services.AddScoped(
            typeof(TService),
            typeof(ApplicationDbContext).Assembly.GetType(
                $"Mart.Customer.Persistence.Repositories.{implementationName}", true)!);

    private static async Task SeedAsync(
        ApplicationDbContext db,
        decimal rewardBalance,
        decimal conversionRate,
        bool includeOtherStoreWallet = false,
        bool includeRewardBucket = true)
    {
        db.WalletTypes.AddRange(
            CreateWalletType(11, "REWARD"),
            CreateWalletType(12, "MART_WALLET"));
        db.MartStores.AddRange(Store(501, true), Store(502, true));
        AddConversionConfiguration(db, 701, 501, 12, conversionRate);

        var now = DateTime.UtcNow;
        var rewardWallet = CustomerWallet.Create(1001, 11, now);
        rewardWallet.Credit(rewardBalance, now);
        db.CustomerWallets.Add(rewardWallet);
        if (includeOtherStoreWallet)
        {
            var otherStoreWallet = CustomerWallet.Create(1001, 12, now, 502);
            otherStoreWallet.Credit(10m, now);
            db.CustomerWallets.Add(otherStoreWallet);
        }
        await db.SaveChangesAsync();

        var source = WalletTransaction.CreateCredit(
            "TX-SEED-REWARD",
            rewardWallet.CustomerWalletId,
            rewardBalance,
            0m,
            rewardBalance,
            "SEED",
            1,
            "Reward test seed",
            now,
            "tests");
        if (includeRewardBucket)
        {
            db.WalletTransactions.Add(source);
            db.WalletBalanceBuckets.Add(WalletBalanceBucket.Create(
                rewardWallet.CustomerWalletId, source, rewardBalance, null, now));
        }
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
    }

    private static WalletType CreateWalletType(int id, string code)
    {
        var type = New<WalletType>();
        Set(type, nameof(WalletType.Id), id);
        Set(type, nameof(WalletType.Name), code);
        Set(type, nameof(WalletType.Code), code);
        Set(type, nameof(WalletType.IsActive), true);
        return type;
    }

    private static MartStoreEntity Store(long id, bool active)
    {
        var store = new MartStoreEntity();
        Set(store, nameof(MartStoreEntity.StoreId), id);
        Set(store, nameof(MartStoreEntity.StoreCode), $"S{id}");
        Set(store, nameof(MartStoreEntity.StoreName), $"Store {id}");
        Set(store, nameof(MartStoreEntity.IsActive), active);
        return store;
    }

    private static void AddConversionConfiguration(
        ApplicationDbContext db,
        long settingId,
        long storeId,
        int walletTypeId,
        decimal rate)
    {
        var setting = New<CashbackConfiguration>();
        Set(setting, nameof(CashbackConfiguration.CashbackSettingId), settingId);
        Set(setting, nameof(CashbackConfiguration.StoreId), (long?)storeId);
        Set(setting, nameof(CashbackConfiguration.IsActive), (bool?)true);
        db.CashbackConfigurations.Add(setting);

        var walletSetting = New<CashbackSettingWallet>();
        Set(walletSetting, nameof(CashbackSettingWallet.Id), checked((int)settingId));
        Set(walletSetting, nameof(CashbackSettingWallet.CashbackSettingId), (long?)settingId);
        Set(walletSetting, nameof(CashbackSettingWallet.WalletTypeId), walletTypeId);
        Set(walletSetting, nameof(CashbackSettingWallet.ConversionRate), (decimal?)rate);
        Set(walletSetting, nameof(CashbackSettingWallet.IsNoExpiry), true);
        Set(walletSetting, nameof(CashbackSettingWallet.IsActive), true);
        db.CashbackSettingWallets.Add(walletSetting);
    }

    private static T New<T>() where T : class =>
        (T)Activator.CreateInstance(typeof(T), nonPublic: true)!;

    private static void Set<T>(T target, string propertyName, object? value) where T : class =>
        typeof(T).GetProperty(propertyName)!.SetValue(target, value);
}
