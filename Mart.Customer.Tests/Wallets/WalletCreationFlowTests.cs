using Mart.Customer.Application;
using Mart.Customer.Application.Abstractions.Data;
using Mart.Customer.Application.Carts.Commands.UpdateCartPaymentStatus;
using Mart.Customer.Application.Customers.Commands.CreateCustomer;
using Mart.Customer.Application.Orders.Commands.CheckoutOrder;
using Mart.Customer.Application.Wallets.Commands.CreditWallet;
using Mart.Customer.Application.Wallets.Commands.EnsureStoreWallet;
using Mart.Customer.Domain.Carts;
using Mart.Customer.Domain.Common;
using Mart.Customer.Domain.Wallets;
using Mart.Customer.Persistence;
using Mart.Customer.Tests.Orders;
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace Mart.Customer.Tests.Wallets;

public sealed class WalletCreationFlowTests
{
    [Theory]
    [InlineData(100, true)]
    [InlineData(1, false)]
    public async Task Checkout_StoreWalletCommitsWithPayment_OrRollsBackWithFailedStock(int stock, bool succeeds)
    {
        await using var fixture = await CheckoutFixture.CreateAsync();
        var cart = await fixture.SeedCartAsync("STORE-WALLET-CHECKOUT", initialStockQuantity: stock);
        var type = (WalletType)Activator.CreateInstance(typeof(WalletType), nonPublic: true)!;
        typeof(WalletType).GetProperty(nameof(WalletType.Id))!.SetValue(type, 3);
        typeof(WalletType).GetProperty(nameof(WalletType.Name))!.SetValue(type, "Mart Wallet");
        typeof(WalletType).GetProperty(nameof(WalletType.Code))!.SetValue(type, "MART_WALLET");
        typeof(WalletType).GetProperty(nameof(WalletType.IsActive))!.SetValue(type, true);
        typeof(WalletType).GetProperty(nameof(WalletType.CreatedDate))!.SetValue(type, DateTime.UtcNow);
        fixture.Db.WalletTypes.Add(type);
        await fixture.Db.SaveChangesAsync();
        var command = new CheckoutOrderCommand(cart.CartNumber, 7, 11, 41, null, null,
            [new CheckoutPayment("Cash", 224.20m, null)]);

        if (succeeds)
        {
            Assert.NotNull(await fixture.CheckoutAsync(command));
            Assert.True((await fixture.CheckoutAsync(command))!.IsIdempotentRetry);
            var wallet = Assert.Single(await fixture.Db.CustomerWallets.AsNoTracking().ToListAsync());
            Assert.Equal(cart.CustomerId, wallet.CustomerId);
            Assert.Equal(cart.MartStoreId, wallet.StoreId);
            Assert.Equal(0m, wallet.CurrentBalance);
        }
        else
        {
            await Assert.ThrowsAsync<DomainException>(() => fixture.CheckoutAsync(command));
            fixture.ClearTracking();
            Assert.Empty(await fixture.Db.CustomerWallets.AsNoTracking().ToListAsync());
            Assert.Empty(await fixture.Db.CustomerOrderPayments.AsNoTracking().ToListAsync());
        }
    }

    [Fact]
    public void SqlServerMapping_UsesUnfilteredUniqueCustomerTypeStoreIndex()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=(local);Database=WalletMapping;Integrated Security=true;TrustServerCertificate=true")
            .Options);
        var index = Assert.Single(db.Model.FindEntityType(typeof(CustomerWallet))!.GetIndexes());
        Assert.True(index.IsUnique);
        Assert.Null(index.GetFilter());
        Assert.Equal(new[] { "CustomerId", "WalletTypeId", "StoreId" }, index.Properties.Select(p => p.Name));
    }

    [Fact]
    public async Task Registration_CreatesOnlyActiveCustomerLevelWallets()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.SeedTypesAsync();

        var customer = await fixture.Sender.Send(CustomerCommand());

        var wallets = await fixture.Db.CustomerWallets.AsNoTracking().ToListAsync();
        Assert.Equal(new[] { 1, 2 }, wallets.Select(w => w.WalletTypeId).Order());
        Assert.All(wallets, wallet =>
        {
            Assert.Equal(customer.CustomerId, wallet.CustomerId);
            Assert.Null(wallet.StoreId);
            Assert.True(wallet.IsActive);
            Assert.Equal(0m, wallet.CurrentBalance);
        });
        Assert.Equal("Wallet customer", customer.DisplayName);
    }

    [Fact]
    public async Task Registration_WalletSaveFailure_RollsBackCustomer()
    {
        var failure = new WalletSaveFailure();
        await using var fixture = await Fixture.CreateAsync(failure);
        await fixture.SeedTypesAsync();
        failure.Enabled = true;

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Sender.Send(CustomerCommand()));

        fixture.Db.ChangeTracker.Clear();
        Assert.Empty(await fixture.Db.Customers.ToListAsync());
        Assert.Empty(await fixture.Db.CustomerWallets.ToListAsync());
    }

    [Fact]
    public async Task Registration_WithoutActiveTypes_StillCreatesCustomer()
    {
        await using var fixture = await Fixture.CreateAsync();
        var customer = await fixture.Sender.Send(CustomerCommand());
        Assert.True(customer.CustomerId > 0);
        Assert.Empty(await fixture.Db.CustomerWallets.ToListAsync());
    }

    [Theory]
    [InlineData("PAID", 1)]
    [InlineData(" paid ", 1)]
    [InlineData("PENDING", 0)]
    [InlineData("EXPIRED", 0)]
    public async Task PaymentConfirmation_OnlyPaidCreatesStoreWallet(string status, int expected)
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.SeedTypesAsync();
        var cart = await fixture.SeedCartAsync(11);

        await fixture.Sender.Send(new UpdateCartPaymentStatusCommand(cart.CustomerCartId, status, 501));
        await fixture.Sender.Send(new UpdateCartPaymentStatusCommand(cart.CustomerCartId, status, 501));

        var wallets = await fixture.Db.CustomerWallets.ToListAsync();
        Assert.Equal(expected, wallets.Count);
        Assert.All(wallets, w => { Assert.Equal(3, w.WalletTypeId); Assert.Equal(11L, w.StoreId); });
    }

    [Fact]
    public async Task PaymentConfirmation_DifferentStores_KeepSeparateWalletsAndBalances()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.SeedTypesAsync();
        foreach (var storeId in new long[] { 11, 12 })
        {
            var cart = await fixture.SeedCartAsync(storeId);
            await fixture.Sender.Send(new UpdateCartPaymentStatusCommand(cart.CustomerCartId, "PAID", 501));
        }
        await fixture.Sender.Send(new CreditWalletCommand(501, 3, 25m, null, null, null, null, "test", 11));
        await fixture.Sender.Send(new EnsureStoreWalletCommand(501, 11));

        var wallets = await fixture.Db.CustomerWallets.AsNoTracking().OrderBy(w => w.StoreId).ToListAsync();
        Assert.Equal(2, wallets.Count);
        Assert.Equal(25m, wallets[0].CurrentBalance);
        Assert.Equal(0m, wallets[1].CurrentBalance);
        var repository = fixture.Services.GetRequiredService<ICustomerWalletRepository>();
        Assert.Null(await repository.GetByCustomerAndTypeAsync(501, 3));
        Assert.Null(await repository.GetByCustomerAndTypeAsync(501, 3, storeId: 99));
        Assert.Equal(wallets[0].CustomerWalletId,
            (await repository.GetDetailAsync(501, 3, storeId: 11))!.CustomerWalletId);
        Assert.Single(await fixture.Db.WalletTransactions.ToListAsync());
    }

    [Fact]
    public async Task PaymentConfirmation_WalletSaveFailure_RollsBackStatusAndWallet()
    {
        var failure = new WalletSaveFailure();
        await using var fixture = await Fixture.CreateAsync(failure);
        await fixture.SeedTypesAsync();
        var cart = await fixture.SeedCartAsync(11);
        failure.Enabled = true;

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Sender.Send(
            new UpdateCartPaymentStatusCommand(cart.CustomerCartId, "PAID", 501)));

        fixture.Db.ChangeTracker.Clear();
        Assert.Empty(await fixture.Db.CustomerWallets.ToListAsync());
        Assert.Contains("PENDING", (await fixture.Db.CustomerCarts.SingleAsync()).Remarks);
    }

    [Fact]
    public async Task PaymentConfirmation_OtherCustomer_CannotCreateWallet()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.SeedTypesAsync();
        var cart = await fixture.SeedCartAsync(11);
        await Assert.ThrowsAsync<DomainException>(() => fixture.Sender.Send(
            new UpdateCartPaymentStatusCommand(cart.CustomerCartId, "PAID", 502)));
        Assert.Empty(await fixture.Db.CustomerWallets.ToListAsync());
    }

    [Fact]
    public async Task EnsureStoreWallet_InactiveType_IsSkipped_AndInactiveWalletIsReused()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.SeedTypesAsync();
        var existing = CustomerWallet.Create(501, 3, DateTime.UtcNow, 11);
        existing.UpdateStatus(false, DateTime.UtcNow);
        fixture.Db.CustomerWallets.Add(existing);
        await fixture.Db.SaveChangesAsync();
        await fixture.Sender.Send(new EnsureStoreWalletCommand(501, 11));
        Assert.False((await fixture.Db.CustomerWallets.SingleAsync()).IsActive);
        var type = await fixture.Db.WalletTypes.SingleAsync(t => t.Id == 3);
        fixture.Db.Entry(type).Property(t => t.IsActive).CurrentValue = false;
        await fixture.Db.SaveChangesAsync();
        await fixture.Sender.Send(new EnsureStoreWalletCommand(501, 12));
        Assert.Single(await fixture.Db.CustomerWallets.ToListAsync());
    }

    [Fact]
    public async Task Database_RejectsDuplicateCustomerTypeStore()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.SeedTypesAsync();
        await fixture.Sender.Send(new EnsureStoreWalletCommand(501, 11));
        fixture.Db.CustomerWallets.Add(CustomerWallet.Create(501, 3, DateTime.UtcNow, 11));
        await Assert.ThrowsAsync<DbUpdateException>(() => fixture.Db.SaveChangesAsync());
    }

    private static CreateCustomerCommand CustomerCommand() => new(
        null, null, null, "Wallet customer", "9876543210", null, null, null,
        null, null, null, null, null, null, null, null, false, false, true, false, null, null);

    private sealed class WalletSaveFailure : SaveChangesInterceptor
    {
        public bool Enabled { get; set; }
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (Enabled && eventData.Context!.ChangeTracker.Entries<CustomerWallet>()
                    .Any(e => e.State == EntityState.Added))
                throw new InvalidOperationException("Simulated wallet save failure.");
            return ValueTask.FromResult(result);
        }
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ServiceProvider _provider;
        private readonly AsyncServiceScope _scope;
        private Fixture(SqliteConnection connection, ServiceProvider provider)
        {
            _connection = connection;
            _provider = provider;
            _scope = provider.CreateAsyncScope();
        }
        public IServiceProvider Services => _scope.ServiceProvider;
        public ApplicationDbContext Db => Services.GetRequiredService<ApplicationDbContext>();
        public ISender Sender => Services.GetRequiredService<ISender>();

        public static async Task<Fixture> CreateAsync(SaveChangesInterceptor? interceptor = null)
        {
            var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=False");
            await connection.OpenAsync();
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddApplication();
            services.AddDbContext<ApplicationDbContext>(options =>
            {
                options.UseSqlite(connection);
                if (interceptor is not null) options.AddInterceptors(interceptor);
            });
            Register<ICustomerRepository>(services, "CustomerRepository");
            Register<ICustomerCartRepository>(services, "CustomerCartRepository");
            Register<ICustomerWalletRepository>(services, "CustomerWalletRepository");
            Register<IWalletTypeRepository>(services, "WalletTypeRepository");
            Register<IWalletTransactionRepository>(services, "WalletTransactionRepository");
            Register<IWalletBalanceBucketRepository>(services, "WalletBalanceBucketRepository");
            Register<IUnitOfWork>(services, "UnitOfWork");
            var fixture = new Fixture(connection, services.BuildServiceProvider());
            await fixture.Db.Database.EnsureCreatedAsync();
            return fixture;
        }

        public async Task SeedTypesAsync()
        {
            foreach (var (id, code, active) in new[]
                     { (1, "REWARD", true), (2, "CASHBACK", true), (3, "MART_WALLET", true), (4, "ARCHIVED", false) })
            {
                var type = (WalletType)Activator.CreateInstance(typeof(WalletType), nonPublic: true)!;
                typeof(WalletType).GetProperty(nameof(WalletType.Id))!.SetValue(type, id);
                typeof(WalletType).GetProperty(nameof(WalletType.Name))!.SetValue(type, code);
                typeof(WalletType).GetProperty(nameof(WalletType.Code))!.SetValue(type, code);
                typeof(WalletType).GetProperty(nameof(WalletType.IsActive))!.SetValue(type, active);
                typeof(WalletType).GetProperty(nameof(WalletType.CreatedDate))!.SetValue(type, DateTime.UtcNow);
                Db.WalletTypes.Add(type);
            }
            await Db.SaveChangesAsync();
        }

        public async Task<CustomerCart> SeedCartAsync(long storeId)
        {
            var cart = CustomerCart.Create(501, 7, storeId, $"PAY-{Guid.NewGuid():N}", 41);
            Db.CustomerCarts.Add(cart);
            Db.Entry(cart).Property(c => c.Remarks).CurrentValue = "{\"status\":\"PENDING\"}";
            await Db.SaveChangesAsync();
            return cart;
        }

        private static void Register<T>(IServiceCollection services, string name) where T : class =>
            services.AddScoped(typeof(T), typeof(ApplicationDbContext).Assembly.GetType(
                $"Mart.Customer.Persistence.Repositories.{name}", throwOnError: true)!);

        public async ValueTask DisposeAsync()
        {
            await _scope.DisposeAsync();
            await _provider.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
