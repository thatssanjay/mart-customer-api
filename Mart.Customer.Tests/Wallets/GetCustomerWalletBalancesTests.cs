using FluentValidation;
using System.Data.Common;
using Mart.Customer.Api.Controllers.V1;
using Mart.Customer.Application;
using Mart.Customer.Application.Abstractions.Data;
using Mart.Customer.Application.Wallets.Dtos;
using Mart.Customer.Application.Wallets.Queries.GetCustomerWalletBalances;
using Mart.Customer.Domain.Wallets;
using Mart.Customer.Persistence;
using Mart.Customer.Persistence.MasterData;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using CustomerEntity = Mart.Customer.Domain.Customers.Customer;

namespace Mart.Customer.Tests.Wallets;

public sealed class GetCustomerWalletBalancesTests
{
    [Fact]
    public async Task Balances_ReturnsActiveTypesAndSeparateActiveStores_InDisplayOrder()
    {
        await using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        AddCustomer(db);
        db.WalletTypes.AddRange(
            Type(1, "REWARD", 2), Type(2, "MISSING", 1),
            Type(3, "MART_WALLET", 3), Type(4, "INACTIVE", 0, false),
            Type(5, "INACTIVE_WALLET", 4));
        db.MartStores.AddRange(Store(101), Store(102), Store(103, false), Store(104));
        db.CustomerWallets.AddRange(
            Wallet(1, 1200.25m), Wallet(3, 250m, 101), Wallet(3, 75.50m, 102),
            Wallet(3, 900m, 103), Wallet(3, 800m, 104, false),
            Wallet(3, 700m, 999), Wallet(3, 600m),
            Wallet(4, 500m), Wallet(5, 400m, active: false),
            Wallet(1, 999m, customerId: 202));
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var walletCountBeforeRead = await db.CustomerWallets.CountAsync();

        var controller = new CustomersController(scope.ServiceProvider.GetRequiredService<ISender>());
        var response = Assert.IsType<OkObjectResult>(
            await controller.GetWalletBalances(1001, CancellationToken.None));
        var result = Assert.IsType<CustomerWalletBalancesDto>(response.Value);

        Assert.Equal(1001, result.CustomerId);
        Assert.Equal(new[] { 2, 1, 3, 3, 5 }, result.Wallets.Select(wallet => wallet.WalletTypeId));
        Assert.Equal(new[] { 0m, 1200.25m, 250m, 75.50m, 0m }, result.Wallets.Select(wallet => wallet.Balance));
        Assert.All(result.Wallets.Where(wallet => wallet.WalletCode != "MART_WALLET"), wallet =>
        {
            Assert.Null(wallet.StoreId);
            Assert.Null(wallet.StoreCode);
            Assert.Null(wallet.StoreName);
        });
        var martWallets = result.Wallets.Where(wallet => wallet.WalletCode == "MART_WALLET").ToList();
        Assert.Equal(new long?[] { 101, 102 }, martWallets.Select(wallet => wallet.StoreId));
        Assert.Equal("S101", martWallets[0].StoreCode);
        Assert.Equal("Store 101", martWallets[0].StoreName);
        Assert.Equal("REWARD wallet", result.Wallets[1].WalletName);
        Assert.Equal(new[] { 1, 2, 3, 3, 4 }, result.Wallets.Select(wallet => wallet.DisplayOrder));
        Assert.Empty(db.ChangeTracker.Entries());
        Assert.Equal(walletCountBeforeRead, await db.CustomerWallets.CountAsync());
        Assert.Empty(await db.WalletTransactions.ToListAsync());
    }

    [Fact]
    public async Task CustomerWithoutWallets_ReturnsZeroGlobalBalancesAndNoMartRow()
    {
        await using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        AddCustomer(db);
        db.WalletTypes.AddRange(Type(1, "REWARD", 1), Type(3, "MART_WALLET", 3));
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var result = await scope.ServiceProvider.GetRequiredService<ISender>()
            .Send(new GetCustomerWalletBalancesQuery(1001));

        Assert.NotNull(result);
        Assert.Equal(0m, Assert.Single(result.Wallets).Balance);
        Assert.Empty(await db.CustomerWallets.ToListAsync());
        Assert.Empty(db.ChangeTracker.Entries());
    }

    [Fact]
    public async Task MissingCustomer_ReturnsNotFound()
    {
        await using var scope = CreateScope();
        var controller = new CustomersController(scope.ServiceProvider.GetRequiredService<ISender>());

        Assert.IsType<NotFoundObjectResult>(
            await controller.GetWalletBalances(999, CancellationToken.None));
    }

    [Fact]
    public async Task NoActiveTypes_ReturnsEmptyWallets()
    {
        await using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        AddCustomer(db);
        db.WalletTypes.Add(Type(1, "INACTIVE", 1, false));
        await db.SaveChangesAsync();

        var result = await scope.ServiceProvider.GetRequiredService<ISender>()
            .Send(new GetCustomerWalletBalancesQuery(1001));

        Assert.NotNull(result);
        Assert.Empty(result.Wallets);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task InvalidCustomerId_IsRejectedByValidationPipeline(long customerId)
    {
        await using var scope = CreateScope();
        await Assert.ThrowsAsync<ValidationException>(() =>
            scope.ServiceProvider.GetRequiredService<ISender>()
                .Send(new GetCustomerWalletBalancesQuery(customerId)));
    }

    [Fact]
    public async Task CancelledRead_PropagatesCancellation()
    {
        await using var scope = CreateScope();
        using var source = new CancellationTokenSource();
        await source.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            scope.ServiceProvider.GetRequiredService<ICustomerWalletRepository>()
                .GetBalancesByCustomerIdAsync(1001, source.Token));
    }

    [Fact]
    public async Task BalanceQuery_TranslatesForSqlServer_WithoutOpeningDatabaseConnection()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=Mart;Trusted_Connection=True")
            .AddInterceptors(new StopBeforeConnectionInterceptor())
            .Options;
        await using var db = new ApplicationDbContext(options);
        var repositoryType = typeof(ApplicationDbContext).Assembly.GetType(
            "Mart.Customer.Persistence.Repositories.CustomerWalletRepository", true)!;
        var repository = (ICustomerWalletRepository)Activator.CreateInstance(repositoryType, db)!;

        // EF translates the full query before opening a connection. A translation
        // failure raises a different exception and fails this assertion.
        await Assert.ThrowsAsync<ConnectionPreventedException>(() =>
            repository.GetBalancesByCustomerIdAsync(1001));
    }

    private sealed class ConnectionPreventedException : Exception;

    private sealed class StopBeforeConnectionInterceptor : DbConnectionInterceptor
    {
        public override ValueTask<InterceptionResult> ConnectionOpeningAsync(
            DbConnection connection,
            ConnectionEventData eventData,
            InterceptionResult result,
            CancellationToken cancellationToken = default) => throw new ConnectionPreventedException();
    }

    private static AsyncServiceScope CreateScope()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddScoped(typeof(ICustomerRepository), typeof(ApplicationDbContext).Assembly.GetType(
            "Mart.Customer.Persistence.Repositories.CustomerRepository", true)!);
        services.AddScoped(typeof(ICustomerWalletRepository), typeof(ApplicationDbContext).Assembly.GetType(
            "Mart.Customer.Persistence.Repositories.CustomerWalletRepository", true)!);
        return services.BuildServiceProvider().CreateAsyncScope();
    }

    private static void AddCustomer(ApplicationDbContext db)
    {
        var customer = (CustomerEntity)Activator.CreateInstance(typeof(CustomerEntity), true)!;
        Set(customer, nameof(CustomerEntity.CustomerId), 1001L);
        Set(customer, nameof(CustomerEntity.MobileNumber), "9999999999");
        db.Customers.Add(customer);
    }

    private static WalletType Type(int id, string code, int order, bool active = true)
    {
        var type = (WalletType)Activator.CreateInstance(typeof(WalletType), true)!;
        Set(type, nameof(WalletType.Id), id);
        Set(type, nameof(WalletType.Code), code);
        Set(type, nameof(WalletType.Name), code + " wallet");
        Set(type, nameof(WalletType.DisplayOrder), order);
        Set(type, nameof(WalletType.IsActive), active);
        return type;
    }

    private static MartStoreEntity Store(long id, bool active = true)
    {
        var store = new MartStoreEntity();
        Set(store, nameof(MartStoreEntity.StoreId), id);
        Set(store, nameof(MartStoreEntity.StoreCode), $"S{id}");
        Set(store, nameof(MartStoreEntity.StoreName), $"Store {id}");
        Set(store, nameof(MartStoreEntity.IsActive), active);
        return store;
    }

    private static CustomerWallet Wallet(
        int typeId, decimal balance, long? storeId = null, bool active = true, long customerId = 1001)
    {
        var wallet = CustomerWallet.Create(customerId, typeId, DateTime.UtcNow, storeId);
        Set(wallet, nameof(CustomerWallet.CurrentBalance), balance);
        Set(wallet, nameof(CustomerWallet.IsActive), active);
        return wallet;
    }

    private static void Set<T>(T target, string name, object value) where T : class =>
        typeof(T).GetProperty(name)!.SetValue(target, value);
}
