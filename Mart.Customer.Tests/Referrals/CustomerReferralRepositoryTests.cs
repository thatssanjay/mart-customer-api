using Mart.Customer.Application.Abstractions.Data;
using Mart.Customer.Application.Referrals.Commands.CreateCustomerReferral;
using Mart.Customer.Domain.Common;
using Mart.Customer.Domain.Referrals;
using Mart.Customer.Domain.Wallets;
using Mart.Customer.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using CustomerEntity = Mart.Customer.Domain.Customers.Customer;

namespace Mart.Customer.Tests.Referrals;

public sealed class CustomerReferralRepositoryTests
{
    private static readonly DateTime CurrentDate = new(2026, 9, 10);

    [Fact]
    public async Task GetActiveBenefitAsync_ReturnsActiveConfigurationAndMapsAllBenefitFields()
    {
        await using var fixture = CreateFixture();
        AddWallet(fixture.Db, 4, true);
        AddConfiguration(
            fixture.Db,
            12,
            true,
            CurrentDate.AddDays(10),
            CurrentDate.AddDays(20),
            500m,
            75m,
            25m,
            4);
        AddConfiguration(fixture.Db, 13, false, CurrentDate.AddDays(-1), CurrentDate.AddDays(1), 1m, 1m, 1m, 4);
        await fixture.Db.SaveChangesAsync();
        fixture.Db.ChangeTracker.Clear();

        var benefit = await fixture.Referrals.GetActiveBenefitAsync();

        Assert.NotNull(benefit);
        Assert.Equal(12, benefit.ReferralConfigId);
        Assert.Equal(500m, benefit.MinimumPurchaseAmount);
        Assert.Equal(75m, benefit.ReferrerRewardPoint);
        Assert.Equal(25m, benefit.ReferredCustomerRewardPoint);
        Assert.Equal(4, benefit.WalletTypeId);
        Assert.Equal("Reward Wallet", benefit.WalletName);
        Assert.Equal("REWARD", benefit.WalletCode);
    }

    [Fact]
    public async Task GetActiveBenefitAsync_ReturnsNullWhenNoConfigurationIsActive()
    {
        await using var fixture = CreateFixture();
        AddWallet(fixture.Db, 4, true);
        AddConfiguration(
            fixture.Db,
            12,
            false,
            CurrentDate.AddDays(-1),
            CurrentDate.AddDays(1),
            500m,
            75m,
            25m,
            4);
        await fixture.Db.SaveChangesAsync();

        Assert.Null(await fixture.Referrals.GetActiveBenefitAsync());
    }

    [Fact]
    public async Task GetActiveBenefitAsync_ReturnsActiveConfigurationWhenWalletIsMissing()
    {
        await using var fixture = CreateFixture();
        AddConfiguration(
            fixture.Db,
            12,
            true,
            CurrentDate,
            null,
            500m,
            75m,
            25m,
            999);
        await fixture.Db.SaveChangesAsync();

        var benefit = await fixture.Referrals.GetActiveBenefitAsync();

        Assert.NotNull(benefit);
        Assert.Equal(12, benefit.ReferralConfigId);
        Assert.Equal(999, benefit.WalletTypeId);
        Assert.Empty(benefit.WalletName);
        Assert.Empty(benefit.WalletCode);
    }

    [Fact]
    public async Task GetActiveConfigurationAsync_RejectsInactiveConfiguration()
    {
        await using var fixture = CreateFixture();
        AddConfiguration(
            fixture.Db,
            12,
            false,
            CurrentDate.AddDays(-1),
            CurrentDate.AddDays(1),
            500m,
            75m,
            25m,
            4);
        await fixture.Db.SaveChangesAsync();

        Assert.Null(await fixture.Referrals.GetActiveConfigurationAsync(12));
    }

    [Fact]
    public async Task GetActiveConfigurationAsync_ReturnsActiveConfigurationRegardlessOfDateRange()
    {
        await using var fixture = CreateFixture();
        AddConfiguration(
            fixture.Db,
            12,
            true,
            CurrentDate.AddDays(10),
            CurrentDate.AddDays(20),
            500m,
            75m,
            25m,
            4);
        await fixture.Db.SaveChangesAsync();

        var configuration = await fixture.Referrals.GetActiveConfigurationAsync(12);

        Assert.NotNull(configuration);
        Assert.True(configuration.IsActive);
    }

    [Fact]
    public async Task CreateReferral_StoresValuesFromValidatedReferralConfiguration()
    {
        await using var fixture = CreateFixture();
        AddCustomer(fixture.Db, 7, "9000000007");
        AddConfiguration(
            fixture.Db,
            12,
            true,
            DateTime.UtcNow.Date.AddDays(-1),
            DateTime.UtcNow.Date,
            500m,
            75m,
            25m,
            4);
        await fixture.Db.SaveChangesAsync();

        var handler = new CreateCustomerReferralCommandHandler(
            fixture.Customers,
            fixture.Referrals,
            fixture.UnitOfWork);
        var created = await handler.Handle(
            new CreateCustomerReferralCommand(7, "9876543210", 12),
            CancellationToken.None);

        Assert.Equal("9876543210", created.ReferredMobileNumber);
        var referral = await fixture.Db.CustomerReferrals.SingleAsync();
        Assert.Equal(12, referral.ReferralConfigId);
        Assert.Equal(500m, referral.MinimumPurchaseAmount);
        Assert.Equal(75m, referral.ReferrerRewardPoint);
        Assert.Equal(25m, referral.ReferredCustomerRewardPoint);
    }

    [Fact]
    public async Task CreateReferral_RejectsInactiveReferralConfigId()
    {
        await using var fixture = CreateFixture();
        AddCustomer(fixture.Db, 7, "9000000007");
        AddConfiguration(
            fixture.Db,
            12,
            false,
            DateTime.UtcNow.Date.AddDays(-1),
            DateTime.UtcNow.Date.AddDays(1),
            500m,
            75m,
            25m,
            4);
        await fixture.Db.SaveChangesAsync();

        var handler = new CreateCustomerReferralCommandHandler(
            fixture.Customers,
            fixture.Referrals,
            fixture.UnitOfWork);
        var error = await Assert.ThrowsAsync<DomainException>(() => handler.Handle(
            new CreateCustomerReferralCommand(7, "9876543210", 12),
            CancellationToken.None));

        Assert.Equal("The referral offer is inactive or has expired.", error.Message);
        Assert.Empty(fixture.Db.CustomerReferrals);
    }

    [Fact]
    public async Task CreateReferral_RejectsUnknownReferralConfigId()
    {
        await using var fixture = CreateFixture();
        AddCustomer(fixture.Db, 7, "9000000007");
        await fixture.Db.SaveChangesAsync();

        var handler = new CreateCustomerReferralCommandHandler(
            fixture.Customers,
            fixture.Referrals,
            fixture.UnitOfWork);
        var error = await Assert.ThrowsAsync<DomainException>(() => handler.Handle(
            new CreateCustomerReferralCommand(7, "9876543210", 999),
            CancellationToken.None));

        Assert.Equal("The referral offer is inactive or has expired.", error.Message);
        Assert.Empty(fixture.Db.CustomerReferrals);
    }

    [Fact]
    public async Task CreateReferral_RejectsExistingCustomerMobile()
    {
        await using var fixture = CreateFixture();
        AddCustomer(fixture.Db, 7, "9000000007");
        AddCustomer(fixture.Db, 8, "9876543210");
        AddActiveConfiguration(fixture.Db);
        await fixture.Db.SaveChangesAsync();

        var error = await Assert.ThrowsAsync<DomainException>(() => CreateReferralAsync(fixture));

        Assert.Equal("This mobile number is already onboarded/referred.", error.Message);
        Assert.Empty(fixture.Db.CustomerReferrals);
    }

    [Fact]
    public async Task CreateReferral_RejectsMobileReferredByAnotherCustomer()
    {
        await using var fixture = CreateFixture();
        AddCustomer(fixture.Db, 7, "9000000007");
        AddCustomer(fixture.Db, 8, "9000000008");
        AddActiveConfiguration(fixture.Db);
        fixture.Db.CustomerReferrals.Add(CustomerReferral.Create(
            8,
            12,
            500m,
            75m,
            25m,
            "9876543210",
            "MARTEXISTING",
            DateTime.UtcNow.AddDays(-1)));
        await fixture.Db.SaveChangesAsync();

        var error = await Assert.ThrowsAsync<DomainException>(() => CreateReferralAsync(fixture));

        Assert.Equal("This mobile number is already onboarded/referred.", error.Message);
        Assert.Single(fixture.Db.CustomerReferrals);
    }

    [Fact]
    public async Task CreateReferral_AllowsNewMobile()
    {
        await using var fixture = CreateFixture();
        AddCustomer(fixture.Db, 7, "9000000007");
        AddActiveConfiguration(fixture.Db);
        await fixture.Db.SaveChangesAsync();

        var created = await CreateReferralAsync(fixture);

        Assert.Equal("9876543210", created.ReferredMobileNumber);
        Assert.Single(fixture.Db.CustomerReferrals);
    }

    private static Task<Mart.Customer.Application.Referrals.Dtos.CreatedCustomerReferralDto> CreateReferralAsync(
        ReferralFixture fixture)
    {
        var handler = new CreateCustomerReferralCommandHandler(
            fixture.Customers,
            fixture.Referrals,
            fixture.UnitOfWork);
        return handler.Handle(
            new CreateCustomerReferralCommand(7, "9876543210", 12),
            CancellationToken.None);
    }

    private static void AddActiveConfiguration(ApplicationDbContext db) =>
        AddConfiguration(
            db,
            12,
            true,
            DateTime.UtcNow.AddDays(10),
            DateTime.UtcNow.AddDays(20),
            500m,
            75m,
            25m,
            4);

    private static ReferralFixture CreateFixture()
    {
        var services = new ServiceCollection();
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        RegisterRepository<ICustomerRepository>(services, "CustomerRepository");
        RegisterRepository<ICustomerReferralRepository>(services, "CustomerReferralRepository");
        RegisterRepository<IUnitOfWork>(services, "UnitOfWork");
        return new ReferralFixture(services.BuildServiceProvider());
    }

    private static void RegisterRepository<T>(IServiceCollection services, string name) where T : class =>
        services.AddScoped(typeof(T), typeof(ApplicationDbContext).Assembly.GetType(
            $"Mart.Customer.Persistence.Repositories.{name}", throwOnError: true)!);

    private static void AddCustomer(ApplicationDbContext db, long id, string mobileNumber)
    {
        var customer = (CustomerEntity)Activator.CreateInstance(typeof(CustomerEntity), nonPublic: true)!;
        Set(customer, nameof(CustomerEntity.CustomerId), id);
        Set(customer, nameof(CustomerEntity.MobileNumber), mobileNumber);
        db.Customers.Add(customer);
    }

    private static void AddWallet(ApplicationDbContext db, int id, bool isActive)
    {
        var wallet = (WalletType)Activator.CreateInstance(typeof(WalletType), nonPublic: true)!;
        Set(wallet, nameof(WalletType.Id), id);
        Set(wallet, nameof(WalletType.Name), "Reward Wallet");
        Set(wallet, nameof(WalletType.Code), "REWARD");
        Set(wallet, nameof(WalletType.IsActive), isActive);
        db.WalletTypes.Add(wallet);
    }

    private static void AddConfiguration(
        ApplicationDbContext db,
        int id,
        bool isActive,
        DateTime startDate,
        DateTime? endDate,
        decimal minimumPurchaseAmount,
        decimal referrerRewardPoint,
        decimal referredCustomerRewardPoint,
        int rewardWalletTypeId)
    {
        var configuration = (ReferralConfiguration)Activator.CreateInstance(
            typeof(ReferralConfiguration),
            nonPublic: true)!;
        Set(configuration, nameof(ReferralConfiguration.Id), id);
        Set(configuration, nameof(ReferralConfiguration.IsActive), isActive);
        Set(configuration, nameof(ReferralConfiguration.StartDate), startDate);
        Set(configuration, nameof(ReferralConfiguration.EndDate), endDate);
        Set(configuration, nameof(ReferralConfiguration.MinimumPurchaseAmount), minimumPurchaseAmount);
        Set(configuration, nameof(ReferralConfiguration.ReferrerRewardPoint), referrerRewardPoint);
        Set(configuration, nameof(ReferralConfiguration.ReferredCustomerRewardPoint), referredCustomerRewardPoint);
        Set(configuration, nameof(ReferralConfiguration.RewardWalletTypeId), rewardWalletTypeId);
        db.ReferralConfigurations.Add(configuration);
    }

    private static void Set(object entity, string propertyName, object? value) =>
        entity.GetType().GetProperty(propertyName)!.SetValue(entity, value);

    private sealed class ReferralFixture(ServiceProvider provider) : IAsyncDisposable
    {
        private readonly AsyncServiceScope _scope = provider.CreateAsyncScope();

        public ApplicationDbContext Db => _scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        public ICustomerRepository Customers => _scope.ServiceProvider.GetRequiredService<ICustomerRepository>();
        public ICustomerReferralRepository Referrals => _scope.ServiceProvider.GetRequiredService<ICustomerReferralRepository>();
        public IUnitOfWork UnitOfWork => _scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        public async ValueTask DisposeAsync()
        {
            await _scope.DisposeAsync();
            await provider.DisposeAsync();
        }
    }
}
