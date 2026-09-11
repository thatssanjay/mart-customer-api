using Mart.Customer.Api.Auth;
using Mart.Customer.Api.Controllers.V1;
using Mart.Customer.Application.Abstractions.Data;
using Mart.Customer.Application.Referrals.Services;
using Mart.Customer.Domain.Orders;
using Mart.Customer.Domain.Referrals;
using Mart.Customer.Domain.Wallets;
using Mart.Customer.Persistence;
using Mart.Customer.Persistence.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Mart.Customer.Tests.Referrals;

public sealed class ProcessReferralRewardsTests
{
    [Fact]
    public async Task ProcessAsync_WhenPaidPurchasesReachThreshold_CreditsBothCustomersAndMarksPaid()
    {
        await using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        SeedWalletType(db, 4, "REWARD", isActive: true);
        SeedConfiguration(db, 12, 100m, 75m, 25m, 4);
        SeedReferral(db, 101, 201, 12);
        SeedWallet(db, 101, 4);
        SeedWallet(db, 201, 4);
        SeedPaidOrder(db, 201, 60m, 1001);
        SeedPaidOrder(db, 201, 40m, 1002);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var result = await scope.ServiceProvider
            .GetRequiredService<IReferralRewardService>()
            .ProcessAsync(9001);

        Assert.Equal(1, result.ActiveReferralCount);
        Assert.Equal(1, result.QualifiedReferralCount);
        Assert.Equal(75m, result.ReferrerPointsCredited);
        Assert.Equal(25m, result.ReferredCustomerPointsCredited);

        var referral = await db.CustomerReferrals.SingleAsync();
        Assert.Equal(CustomerReferral.PaidStatus, referral.Status);
        Assert.True(referral.RewardProcessed);
        Assert.NotNull(referral.QualifiedDate);
        Assert.Equal(referral.QualifiedDate, referral.RewardedDate);

        var balances = await db.CustomerWallets
            .OrderBy(wallet => wallet.CustomerId)
            .Select(wallet => wallet.CurrentBalance)
            .ToListAsync();
        Assert.Equal([75m, 25m], balances);
        Assert.Equal(2, await db.WalletTransactions.CountAsync(transaction =>
            transaction.TransactionType == "CREDIT" &&
            transaction.ReferenceType == "REFERRAL" &&
            transaction.ReferenceId == referral.CustomerReferralId));
        Assert.Equal(2, await db.WalletBalanceBuckets.CountAsync());
        Assert.All(await db.WalletTransactions.ToListAsync(), transaction =>
            Assert.Equal("9001", transaction.CreatedBy));
    }

    [Fact]
    public async Task ProcessAsync_WhenCalledAgain_DoesNotRewardReferralTwice()
    {
        await using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        SeedWalletType(db, 4, "REWARD", isActive: true);
        SeedConfiguration(db, 12, 50m, 10m, 5m, 4);
        SeedReferral(db, 101, 201, 12);
        SeedWallet(db, 101, 4);
        SeedWallet(db, 201, 4);
        SeedPaidOrder(db, 201, 50m, 1001);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var service = scope.ServiceProvider.GetRequiredService<IReferralRewardService>();

        await service.ProcessAsync(9001);
        var repeated = await service.ProcessAsync(9001);

        Assert.Equal(0, repeated.QualifiedReferralCount);
        Assert.Equal(2, await db.WalletTransactions.CountAsync());
        Assert.Equal(15m, await db.CustomerWallets.SumAsync(wallet => wallet.CurrentBalance));
    }

    [Fact]
    public async Task ProcessAsync_WhenThresholdIsNotReached_LeavesReferralAndWalletsUnchanged()
    {
        await using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        SeedWalletType(db, 4, "REWARD", isActive: true);
        SeedConfiguration(db, 12, 100m, 75m, 25m, 4);
        SeedReferral(db, 101, 201, 12);
        SeedWallet(db, 101, 4);
        SeedWallet(db, 201, 4);
        SeedPaidOrder(db, 201, 99.99m, 1001);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var result = await scope.ServiceProvider
            .GetRequiredService<IReferralRewardService>()
            .ProcessAsync(9001);

        Assert.Equal(1, result.ActiveReferralCount);
        Assert.Equal(0, result.QualifiedReferralCount);
        var referral = await db.CustomerReferrals.SingleAsync();
        Assert.Equal(CustomerReferral.ActiveStatus, referral.Status);
        Assert.False(referral.RewardProcessed);
        Assert.Empty(await db.WalletTransactions.ToListAsync());
    }

    [Fact]
    public void ProcessRewardsEndpoint_RequiresExistingMartAdminPolicy()
    {
        var method = typeof(ReferralsController).GetMethod(nameof(ReferralsController.ProcessRewards));

        var authorization = Assert.Single(method!.GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>());
        Assert.Equal(MartAuthorizationPolicies.MartAdmin, authorization.Policy);
    }

    private static AsyncServiceScope CreateScope()
    {
        var services = new ServiceCollection();
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddScoped<IUnitOfWork>(provider =>
            (IUnitOfWork)Activator.CreateInstance(
                typeof(ApplicationDbContext).Assembly.GetType(
                    "Mart.Customer.Persistence.Repositories.UnitOfWork",
                    throwOnError: true)!,
                provider.GetRequiredService<ApplicationDbContext>())!);
        services.AddScoped<IReferralRewardService, ReferralRewardService>();
        return services.BuildServiceProvider().CreateAsyncScope();
    }

    private static void SeedReferral(ApplicationDbContext db, long referrerId, long referredId, int configId)
    {
        var referral = CustomerReferral.Create(
            referrerId,
            configId,
            1m,
            1m,
            1m,
            "9876543210",
            Guid.NewGuid().ToString("N")[..12],
            DateTime.UtcNow.AddDays(-1));
        referral.MarkOnboarded(referredId, DateTime.UtcNow.AddHours(-1));
        db.CustomerReferrals.Add(referral);
    }

    private static void SeedConfiguration(
        ApplicationDbContext db,
        int id,
        decimal minimumPurchase,
        decimal referrerPoints,
        decimal referredPoints,
        int walletTypeId)
    {
        var configuration = (ReferralConfiguration)Activator.CreateInstance(
            typeof(ReferralConfiguration), nonPublic: true)!;
        Set(configuration, nameof(ReferralConfiguration.Id), id);
        Set(configuration, nameof(ReferralConfiguration.MinimumPurchaseAmount), minimumPurchase);
        Set(configuration, nameof(ReferralConfiguration.ReferrerRewardPoint), referrerPoints);
        Set(configuration, nameof(ReferralConfiguration.ReferredCustomerRewardPoint), referredPoints);
        Set(configuration, nameof(ReferralConfiguration.RewardWalletTypeId), walletTypeId);
        Set(configuration, nameof(ReferralConfiguration.StartDate), DateTime.UtcNow.AddDays(-10));
        Set(configuration, nameof(ReferralConfiguration.IsActive), true);
        db.ReferralConfigurations.Add(configuration);
    }

    private static void SeedWalletType(ApplicationDbContext db, int id, string code, bool isActive)
    {
        var walletType = (WalletType)Activator.CreateInstance(typeof(WalletType), nonPublic: true)!;
        Set(walletType, nameof(WalletType.Id), id);
        Set(walletType, nameof(WalletType.Name), "Referral Rewards");
        Set(walletType, nameof(WalletType.Code), code);
        Set(walletType, nameof(WalletType.IsActive), isActive);
        Set(walletType, nameof(WalletType.CreatedDate), DateTime.UtcNow);
        db.WalletTypes.Add(walletType);
    }

    private static void SeedWallet(ApplicationDbContext db, long customerId, int walletTypeId) =>
        db.CustomerWallets.Add(CustomerWallet.Create(customerId, walletTypeId, DateTime.UtcNow));

    private static void SeedPaidOrder(ApplicationDbContext db, long customerId, decimal amount, long cartId)
    {
        db.CustomerOrders.Add(CustomerOrder.Create(
            cartId,
            $"INV-{cartId}",
            customerId,
            franchiseId: 1,
            martStoreId: 1,
            DateTime.UtcNow,
            totalItemCount: 1,
            grossAmount: amount,
            discountAmount: 0,
            gstAmount: 0,
            netAmount: amount,
            redemptionWalletTypeId: null,
            redemptionAmount: 0,
            finalPayableAmount: amount,
            createdBy: 9001));
    }

    private static void Set(object entity, string propertyName, object? value) =>
        entity.GetType().GetProperty(propertyName)!.SetValue(entity, value);
}
