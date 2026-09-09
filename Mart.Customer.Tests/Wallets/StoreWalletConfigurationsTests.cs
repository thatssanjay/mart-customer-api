using System.Net;
using System.Net.Http.Json;
using System.Data.Common;
using FluentValidation.TestHelper;
using Mart.Customer.Application.Abstractions.Data;
using Mart.Customer.Api.Controllers.V1;
using Mart.Customer.Application.Cashback.Dtos;
using Mart.Customer.Application.Cashback.Queries.GetStoreWalletConfigurations;
using Mart.Customer.Domain.Cashback;
using Mart.Customer.Domain.Wallets;
using Mart.Customer.Persistence;
using Mart.Customer.Persistence.MasterData;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace Mart.Customer.Tests.Wallets;

public sealed class StoreWalletConfigurationsTests
{
    [Fact]
    public async Task Endpoint_ReturnsNewestConfigurationPerStoreOrderedByRateDescendingAcrossPages()
    {
        await using var factory = new GetCustomerWalletsApiFactory();
        await SeedAsync(factory, db =>
        {
            db.WalletTypes.Add(CreateWalletType(801, "Store balance", "MART_WALLET"));
            db.MartStores.AddRange(
                Store(101, "JJ Store Kharadi", "Pune"),
                Store(102, "JJ Store Baner", "Pune"));

            AddConfiguration(db, 1001, 101, 801, 2m);
            AddConfiguration(db, 1002, 101, 801, 5m);
            AddConfiguration(db, 1003, 102, 801, 7.5m);
        });

        using var client = factory.CreateClient();
        var firstPage = await client.GetFromJsonAsync<StoreWalletConfigurationsPageDto>(
            "/api/v1/customers/store-wallet-configurations?pageNumber=1&pageSize=1");
        var secondPage = await client.GetFromJsonAsync<StoreWalletConfigurationsPageDto>(
            "/api/v1/customers/store-wallet-configurations?pageNumber=2&pageSize=1");

        Assert.NotNull(firstPage);
        Assert.Equal(1, firstPage.PageNumber);
        Assert.Equal(1, firstPage.PageSize);
        Assert.Equal(2, firstPage.TotalRecords);
        Assert.True(firstPage.HasNextPage);
        var first = Assert.Single(firstPage.Items);
        Assert.Equal(102, first.StoreId);
        Assert.Equal("JJ Store Baner", first.StoreName);
        Assert.Equal("Pune", first.City);
        Assert.Equal(7.5m, first.ConversionRate);

        Assert.NotNull(secondPage);
        Assert.Equal(2, secondPage.TotalRecords);
        Assert.False(secondPage.HasNextPage);
        Assert.Equal(101, Assert.Single(secondPage.Items).StoreId);
        Assert.Equal(5m, Assert.Single(secondPage.Items).ConversionRate);
    }

    [Fact]
    public async Task Endpoint_ReturnsOnlyCurrentActiveMartWalletConfigurations()
    {
        await using var factory = new GetCustomerWalletsApiFactory();
        var now = DateTime.UtcNow;
        await SeedAsync(factory, db =>
        {
            db.WalletTypes.AddRange(
                CreateWalletType(811, "Any display name", "MART_WALLET"),
                CreateWalletType(812, "MART_WALLET", "OTHER_WALLET"),
                CreateWalletType(813, "Inactive Mart Wallet", "MART_WALLET", false));

            for (var storeId = 201L; storeId <= 211; storeId++)
                db.MartStores.Add(Store(storeId, $"Store {storeId}", $"City {storeId}", storeId != 202));

            AddConfiguration(db, 2001, 201, 811, 4m);
            AddConfiguration(db, 2002, 202, 811, 4m);
            AddConfiguration(db, 2003, 203, 811, 4m, settingActive: false);
            AddConfiguration(db, 2004, 204, 811, 4m, settingStart: now.AddDays(1));
            AddConfiguration(db, 2005, 205, 811, 4m, settingEnd: now.AddDays(-1));
            AddConfiguration(db, 2006, 206, 811, 4m, walletActive: false);
            AddConfiguration(db, 2007, 207, 811, 4m, isNoExpiry: false, walletEnd: now.AddDays(-1));
            AddConfiguration(db, 2008, 208, 811, 4m, isNoExpiry: false, walletStart: now.AddDays(1));
            AddConfiguration(db, 2009, 209, 812, 4m);
            AddConfiguration(db, 2010, 210, 813, 4m);
            AddConfiguration(db, 2011, 211, 811, null);
        });

        using var client = factory.CreateClient();
        using var response = await client.GetAsync(
            "/api/v1/customers/store-wallet-configurations?pageNumber=1&pageSize=100");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<StoreWalletConfigurationsPageDto>();
        Assert.NotNull(result);
        Assert.Equal(1, result.TotalRecords);
        Assert.False(result.HasNextPage);
        Assert.Equal(201, Assert.Single(result.Items).StoreId);
    }

    [Fact]
    public void Endpoint_UsesExpectedVersionedCustomerRoute()
    {
        var method = typeof(CustomersController)
            .GetMethod(nameof(CustomersController.GetStoreWalletConfigurations));
        var route = Assert.Single(method!.GetCustomAttributes(typeof(HttpGetAttribute), false)
            .Cast<HttpGetAttribute>());

        Assert.Equal("store-wallet-configurations", route.Template);
    }

    [Fact]
    public void Validator_RejectsInvalidPagination()
    {
        var validator = new GetStoreWalletConfigurationsQueryValidator();

        var result = validator.TestValidate(new GetStoreWalletConfigurationsQuery(0, 101));

        result.ShouldHaveValidationErrorFor(query => query.PageNumber);
        result.ShouldHaveValidationErrorFor(query => query.PageSize);
    }

    [Fact]
    public async Task Endpoint_ReturnsStandardValidationProblemForInvalidPagination()
    {
        await using var factory = new GetCustomerWalletsApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(
            "/api/v1/customers/store-wallet-configurations?pageNumber=0&pageSize=101");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal("Validation failed", problem.Title);
    }

    [Fact]
    public async Task RepositoryQuery_TranslatesForSqlServerBeforeOpeningAConnection()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=Mart;Trusted_Connection=True")
            .AddInterceptors(new StopBeforeConnectionInterceptor())
            .Options;
        await using var db = new ApplicationDbContext(options);
        var repositoryType = typeof(ApplicationDbContext).Assembly.GetType(
            "Mart.Customer.Persistence.Repositories.CashbackConfigurationRepository", true)!;
        var repository = (ICashbackConfigurationRepository)Activator.CreateInstance(repositoryType, db)!;

        await Assert.ThrowsAsync<ConnectionPreventedException>(() =>
            repository.GetStoreWalletConfigurationsPagedAsync(
                "MART_WALLET", DateTime.UtcNow, 1, 20));
    }

    private static async Task SeedAsync(
        GetCustomerWalletsApiFactory factory,
        Action<ApplicationDbContext> seed)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        seed(db);
        await db.SaveChangesAsync();
    }

    private static MartStoreEntity Store(
        long id,
        string name,
        string city,
        bool active = true)
    {
        var store = new MartStoreEntity();
        Set(store, nameof(MartStoreEntity.StoreId), id);
        Set(store, nameof(MartStoreEntity.FranchiseId), 1L);
        Set(store, nameof(MartStoreEntity.StoreName), name);
        Set(store, nameof(MartStoreEntity.City), city);
        Set(store, nameof(MartStoreEntity.IsActive), active);
        return store;
    }

    private static WalletType CreateWalletType(int id, string name, string code, bool active = true)
    {
        var walletType = New<WalletType>();
        Set(walletType, nameof(WalletType.Id), id);
        Set(walletType, nameof(WalletType.Name), name);
        Set(walletType, nameof(WalletType.Code), code);
        Set(walletType, nameof(WalletType.IsActive), active);
        return walletType;
    }

    private static void AddConfiguration(
        ApplicationDbContext db,
        long settingId,
        long storeId,
        int walletTypeId,
        decimal? conversionRate,
        bool settingActive = true,
        DateTime? settingStart = null,
        DateTime? settingEnd = null,
        bool walletActive = true,
        bool isNoExpiry = true,
        DateTime? walletStart = null,
        DateTime? walletEnd = null)
    {
        var setting = New<CashbackConfiguration>();
        Set(setting, nameof(CashbackConfiguration.CashbackSettingId), settingId);
        Set(setting, nameof(CashbackConfiguration.StoreId), (long?)storeId);
        Set(setting, nameof(CashbackConfiguration.IsActive), (bool?)settingActive);
        Set(setting, nameof(CashbackConfiguration.StartDate), settingStart);
        Set(setting, nameof(CashbackConfiguration.EndDate), settingEnd);
        db.CashbackConfigurations.Add(setting);

        var walletSetting = New<CashbackSettingWallet>();
        Set(walletSetting, nameof(CashbackSettingWallet.Id), checked((int)settingId));
        Set(walletSetting, nameof(CashbackSettingWallet.CashbackSettingId), (long?)settingId);
        Set(walletSetting, nameof(CashbackSettingWallet.WalletTypeId), walletTypeId);
        Set(walletSetting, nameof(CashbackSettingWallet.ConversionRate), (decimal?)conversionRate);
        Set(walletSetting, nameof(CashbackSettingWallet.IsActive), walletActive);
        Set(walletSetting, nameof(CashbackSettingWallet.IsNoExpiry), isNoExpiry);
        Set(walletSetting, nameof(CashbackSettingWallet.StartDate), walletStart);
        Set(walletSetting, nameof(CashbackSettingWallet.EndDate), walletEnd);
        db.CashbackSettingWallets.Add(walletSetting);
    }

    private static T New<T>() where T : class =>
        (T)Activator.CreateInstance(typeof(T), nonPublic: true)!;

    private static void Set<T>(T target, string propertyName, object? value) where T : class =>
        typeof(T).GetProperty(propertyName)!.SetValue(target, value);

    private sealed class ConnectionPreventedException : Exception;

    private sealed class StopBeforeConnectionInterceptor : DbConnectionInterceptor
    {
        public override ValueTask<InterceptionResult> ConnectionOpeningAsync(
            DbConnection connection,
            ConnectionEventData eventData,
            InterceptionResult result,
            CancellationToken cancellationToken = default) => throw new ConnectionPreventedException();
    }
}
