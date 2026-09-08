using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Mart.Customer.Application;
using Mart.Customer.Application.Abstractions.Data;
using Mart.Customer.Application.Orders.Commands.CheckoutOrder;
using Mart.Customer.Application.Orders.Dtos;
using Mart.Customer.Application.Orders.Services;
using Mart.Customer.Application.Inventory.Services;
using Mart.Customer.Domain.Common;
using Mart.Customer.Domain.Cashback;
using Mart.Customer.Domain.Inventory;
using Mart.Customer.Domain.Subscriptions;
using Mart.Customer.Domain.Wallets;
using Mart.Customer.Persistence;
using Mart.Customer.Persistence.Services;
using Mart.Customer.Tests.Wallets;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Mart.Customer.Tests.Orders;

public sealed class OrderCheckoutServiceTests
{
    [Fact]
    public void OrderSnapshots_ApplyServerProductIdentityAndIntraStateTaxSplit()
    {
        var order = CreateSnapshotOrder();
        var item = Assert.Single(order.Items);

        item.ApplyProductSnapshot("SKU-501", "Master product name");
        order.ApplyTaxSplit(isIntraState: true);

        Assert.Equal("SKU-501", item.ProductCodeSnapshot);
        Assert.Equal("Master product name", item.ProductNameSnapshot);
        Assert.Equal(9m, item.CGSTAmount);
        Assert.Equal(9m, item.SGSTAmount);
        Assert.Equal(0m, item.IGSTAmount);
        Assert.Equal(9m, order.CGSTAmount);
        Assert.Equal(9m, order.SGSTAmount);
        Assert.Equal(0m, order.IGSTAmount);
    }

    [Fact]
    public void OrderSnapshots_ApplyInterStateTaxSplit()
    {
        var order = CreateSnapshotOrder();

        order.ApplyTaxSplit(isIntraState: false);

        var item = Assert.Single(order.Items);
        Assert.Equal(0m, item.CGSTAmount);
        Assert.Equal(0m, item.SGSTAmount);
        Assert.Equal(18m, item.IGSTAmount);
        Assert.Equal(18m, order.IGSTAmount);
    }

    [Fact]
    public async Task Checkout_WithCash_CreatesPermanentSnapshotsAndPaysCart()
    {
        await using var fixture = await CheckoutFixture.CreateAsync();
        var cart = await fixture.SeedCartAsync("CART-CHECKOUT-CASH");

        var result = await fixture.CheckoutAsync(Command(cart.CartNumber, Cash(224.20m)));

        Assert.NotNull(result);
        Assert.Equal("Paid", result.OrderStatus);
        Assert.Equal("Archived", result.InvoiceStatus);
        Assert.Single(result.Items);
        Assert.Equal("Cash", Assert.Single(result.Payments).PaymentMode);
        Assert.StartsWith("INV-", result.InvoiceNumber);
        Assert.False(result.IsIdempotentRetry);
        fixture.ClearTracking();
        Assert.Equal("Paid", await fixture.Db.CustomerCarts.AsNoTracking().Select(item => item.CartStatus).SingleAsync());
        Assert.NotNull(await fixture.Db.CustomerCarts.AsNoTracking().Select(item => item.PaidOn).SingleAsync());
        Assert.Equal(98m, await fixture.Db.StoreStocks.AsNoTracking().Select(item => item.CurrentQuantity).SingleAsync());
        var movement = Assert.Single(await fixture.Db.StockMovements.AsNoTracking().ToListAsync());
        Assert.Equal("SALE", movement.MovementType);
        Assert.Equal("ORDER", movement.ReferenceType);
        Assert.Equal(result.CustomerOrderId, movement.ReferenceId);
        Assert.Equal(2m, movement.Quantity);
        Assert.Equal(100m, movement.PreviousQuantity);
        Assert.Equal(98m, movement.NewQuantity);
        Assert.Equal(41, movement.CreatedBy);
        Assert.Empty(await fixture.Db.CustomerWallets.AsNoTracking().ToListAsync());
        Assert.Empty(await fixture.Db.WalletTransactions.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task Checkout_WithUpi_StoresValidatedReference()
    {
        await using var fixture = await CheckoutFixture.CreateAsync();
        var cart = await fixture.SeedCartAsync("CART-CHECKOUT-UPI");

        var result = await fixture.CheckoutAsync(Command(cart.CartNumber, Upi(224.20m, "upi-001")));

        Assert.NotNull(result);
        var payment = Assert.Single(result.Payments);
        Assert.Equal("UPI", payment.PaymentMode);
        Assert.Equal("upi-001", payment.TransactionReference);
    }

    [Fact]
    public async Task Checkout_WithCard_StoresValidatedReference()
    {
        await using var fixture = await CheckoutFixture.CreateAsync();
        var cart = await fixture.SeedCartAsync("CART-CHECKOUT-CARD");

        var result = await fixture.CheckoutAsync(Command(
            cart.CartNumber,
            Card(224.20m, "card-001")));

        Assert.NotNull(result);
        var payment = Assert.Single(result.Payments);
        Assert.Equal("Card", payment.PaymentMode);
        Assert.Equal("card-001", payment.TransactionReference);
    }

    [Fact]
    public async Task Checkout_WithCardAndNoReference_IsRejectedWithoutWrites()
    {
        await using var fixture = await CheckoutFixture.CreateAsync();
        var cart = await fixture.SeedCartAsync("CART-CHECKOUT-CARD-NO-REFERENCE");

        var exception = await Assert.ThrowsAsync<DomainException>(() =>
            fixture.CheckoutAsync(Command(
                cart.CartNumber,
                new CheckoutPayment("Card", 224.20m, null))));

        Assert.Equal("A transaction reference is required for CARD payments.", exception.Message);
        Assert.Empty(await fixture.Db.CustomerOrders.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task Checkout_WithMixedPayment_CreatesBothPaymentRows()
    {
        await using var fixture = await CheckoutFixture.CreateAsync();
        var cart = await fixture.SeedCartAsync("CART-CHECKOUT-MIXED");

        var result = await fixture.CheckoutAsync(Command(
            cart.CartNumber,
            Cash(100m),
            Upi(124.20m, "upi-mixed")));

        Assert.NotNull(result);
        Assert.Equal(2, result.Payments.Count);
        Assert.Equal(224.20m, result.Payments.Sum(payment => payment.Amount));
    }

    [Theory]
    [InlineData("Cash", null)]
    [InlineData("POS", "pos-001")]
    [InlineData("UPI", "upi-001")]
    [InlineData("Wallet", "wallet-001")]
    public async Task Checkout_WithNonAppPayment_IgnoresWalletInputsAndDoesNotTouchWallet(
        string paymentMode,
        string? reference)
    {
        await using var fixture = await CheckoutFixture.CreateAsync();
        var cart = await fixture.SeedCartAsync($"CART-CHECKOUT-NON-APP-{paymentMode}");
        await fixture.SeedWalletAsync(cart.CustomerId, 3091, 20m, "CASHBACK");
        await fixture.SeedActiveSubscriptionAsync(cart.CustomerId, 3091, 10m);
        await fixture.SeedCashbackAsync(10m, 15m);

        var command = new CheckoutOrderCommand(
            cart.CartNumber,
            7,
            11,
            41,
            3091,
            30m,
            "invalid-wallet-token-that-must-not-be-validated",
            [new CheckoutPayment(paymentMode, 224.20m, reference)]);

        var result = await fixture.CheckoutAsync(command);

        Assert.NotNull(result);
        Assert.Equal(0m, result.RedemptionAmount);
        Assert.Equal(224.20m, result.FinalPayableAmount);
        Assert.Equal(0m, result.RewardEarned);
        Assert.Equal(0m, result.CashbackEarned);
        fixture.ClearTracking();
        Assert.Equal(20m, await fixture.Db.CustomerWallets.AsNoTracking()
            .Select(wallet => wallet.CurrentBalance)
            .SingleAsync());
        Assert.Empty(await fixture.Db.WalletTransactions.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task Checkout_WithRedemption_DebitsWalletInsideCheckoutTransaction()
    {
        await using var fixture = await CheckoutFixture.CreateAsync();
        var cart = await fixture.SeedCartAsync("CART-CHECKOUT-REDEEM");
        await fixture.SeedWalletAsync(cart.CustomerId, 3101, 100m);
        var appPayment = await fixture.PrepareAppPaymentAsync(cart.CustomerCartId, 174.20m);

        var result = await fixture.CheckoutAsync(new CheckoutOrderCommand(
            cart.CartNumber, 7, 11, 41, 3101, 50m, appPayment.Token,
            [App(174.20m, appPayment.Reference)]));

        Assert.NotNull(result);
        Assert.Equal(50m, result.RedemptionAmount);
        fixture.ClearTracking();
        Assert.Equal(50m, await fixture.Db.CustomerWallets.AsNoTracking().Select(wallet => wallet.CurrentBalance).SingleAsync());
        Assert.Single(await fixture.Db.WalletTransactions.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task Checkout_WithInsufficientWallet_RollsBackOrderPaymentsAndCartChange()
    {
        await using var fixture = await CheckoutFixture.CreateAsync();
        var cart = await fixture.SeedCartAsync("CART-CHECKOUT-INSUFFICIENT");
        await fixture.SeedWalletAsync(cart.CustomerId, 3201, 20m);
        var appPayment = await fixture.PrepareAppPaymentAsync(cart.CustomerCartId, 194.20m);

        var exception = await Assert.ThrowsAsync<DomainException>(() => fixture.CheckoutAsync(
            new CheckoutOrderCommand(cart.CartNumber, 7, 11, 41, 3201, 30m, appPayment.Token,
                [App(194.20m, appPayment.Reference)])));

        Assert.Equal("Insufficient wallet balance.", exception.Message);
        fixture.ClearTracking();
        Assert.Empty(await fixture.Db.CustomerOrders.AsNoTracking().ToListAsync());
        Assert.Empty(await fixture.Db.CustomerOrderPayments.AsNoTracking().ToListAsync());
        Assert.Equal("Active", await fixture.Db.CustomerCarts.AsNoTracking().Select(item => item.CartStatus).SingleAsync());
    }

    [Fact]
    public async Task Checkout_WhenPaymentSumDoesNotMatch_RejectsWithoutWrites()
    {
        await using var fixture = await CheckoutFixture.CreateAsync();
        var cart = await fixture.SeedCartAsync("CART-CHECKOUT-MISMATCH");

        var exception = await Assert.ThrowsAsync<DomainException>(() =>
            fixture.CheckoutAsync(Command(cart.CartNumber, Cash(200m))));

        Assert.Equal("Payment total must equal the final payable amount.", exception.Message);
        Assert.Empty(await fixture.Db.CustomerOrders.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task Checkout_WithInsufficientStock_RollsBackOrderAndLeavesInventoryUnchanged()
    {
        await using var fixture = await CheckoutFixture.CreateAsync();
        var cart = await fixture.SeedCartAsync(
            "CART-CHECKOUT-INSUFFICIENT-STOCK",
            initialStockQuantity: 1m);

        var exception = await Assert.ThrowsAsync<DomainException>(() =>
            fixture.CheckoutAsync(Command(cart.CartNumber, Cash(224.20m))));

        Assert.Equal("Insufficient store stock for product 101.", exception.Message);
        fixture.ClearTracking();
        Assert.Empty(await fixture.Db.CustomerOrders.AsNoTracking().ToListAsync());
        Assert.Empty(await fixture.Db.CustomerOrderItems.AsNoTracking().ToListAsync());
        Assert.Empty(await fixture.Db.StockMovements.AsNoTracking().ToListAsync());
        Assert.Equal(1m, await fixture.Db.StoreStocks.AsNoTracking().Select(item => item.CurrentQuantity).SingleAsync());
        Assert.Equal("Active", await fixture.Db.CustomerCarts.AsNoTracking().Select(item => item.CartStatus).SingleAsync());
    }

    [Fact]
    public async Task Checkout_WithExpiryManagedProduct_DeductsBatchesByFefo()
    {
        await using var fixture = await CheckoutFixture.CreateAsync();
        var cart = await fixture.SeedCartAsync(
            "CART-CHECKOUT-BATCH",
            initialStockQuantity: 9m,
            isBatchApplicable: true,
            isExpiryApplicable: true,
            batches:
            [
                new BatchSeed("LATE", 5m, DateTime.UtcNow.Date.AddDays(30), DateTime.UtcNow.Date.AddDays(-30)),
                new BatchSeed("FIRST", 1m, DateTime.UtcNow.Date.AddDays(5), DateTime.UtcNow.Date.AddDays(-10)),
                new BatchSeed("SECOND", 3m, DateTime.UtcNow.Date.AddDays(10), DateTime.UtcNow.Date.AddDays(-20))
            ]);

        var result = await fixture.CheckoutAsync(Command(cart.CartNumber, Cash(224.20m)));

        Assert.NotNull(result);
        fixture.ClearTracking();
        Assert.Equal(7m, await fixture.Db.StoreStocks.AsNoTracking().Select(item => item.CurrentQuantity).SingleAsync());
        var batches = await fixture.Db.ProductBatchStocks
            .AsNoTracking()
            .ToDictionaryAsync(item => item.BatchNumber!);
        Assert.Equal(0m, batches["FIRST"].Quantity);
        Assert.Equal(2m, batches["SECOND"].Quantity);
        Assert.Equal(5m, batches["LATE"].Quantity);
        Assert.Single(await fixture.Db.StockMovements.AsNoTracking()
            .Where(item => item.MovementType == "SALE")
            .ToListAsync());
    }

    [Fact]
    public async Task Checkout_WhenRepeatedWithSameState_ReturnsExistingOrder()
    {
        await using var fixture = await CheckoutFixture.CreateAsync();
        var cart = await fixture.SeedCartAsync("CART-CHECKOUT-RETRY");
        var command = Command(cart.CartNumber, Cash(224.20m));

        var first = await fixture.CheckoutAsync(command);
        fixture.ClearTracking();
        var retry = await fixture.CheckoutAsync(command);

        Assert.NotNull(first);
        Assert.NotNull(retry);
        Assert.Equal(first.CustomerOrderId, retry.CustomerOrderId);
        Assert.True(retry.IsIdempotentRetry);
        fixture.ClearTracking();
        Assert.Equal(1, await fixture.Db.CustomerOrders.AsNoTracking().CountAsync());
        Assert.Equal(98m, await fixture.Db.StoreStocks.AsNoTracking().Select(item => item.CurrentQuantity).SingleAsync());
        Assert.Equal(1, await fixture.Db.StockMovements.AsNoTracking()
            .Where(item => item.MovementType == "SALE")
            .CountAsync());
    }

    [Fact]
    public async Task Checkout_WhenRedemptionIsRepeated_ReturnsExistingOrder()
    {
        await using var fixture = await CheckoutFixture.CreateAsync();
        var cart = await fixture.SeedCartAsync("CART-CHECKOUT-REDEEM-RETRY");
        await fixture.SeedWalletAsync(cart.CustomerId, 3151, 100m);
        var appPayment = await fixture.PrepareAppPaymentAsync(cart.CustomerCartId, 174.20m);
        var command = new CheckoutOrderCommand(
            cart.CartNumber, 7, 11, 41, 3151, 50m, appPayment.Token,
            [App(174.20m, appPayment.Reference)]);

        var first = await fixture.CheckoutAsync(command);
        fixture.ClearTracking();
        var retry = await fixture.CheckoutAsync(command);

        Assert.NotNull(first);
        Assert.NotNull(retry);
        Assert.Equal(first.CustomerOrderId, retry.CustomerOrderId);
        Assert.True(retry.IsIdempotentRetry);
        fixture.ClearTracking();
        Assert.Equal(1, await fixture.Db.CustomerOrders.AsNoTracking().CountAsync());
        Assert.Equal(1, await fixture.Db.WalletTransactions.AsNoTracking().CountAsync());
    }

    [Fact]
    public async Task Checkout_WithActiveSubscription_CreditsRewardWallet()
    {
        await using var fixture = await CheckoutFixture.CreateAsync();
        var cart = await fixture.SeedCartAsync("CART-CHECKOUT-REWARD");
        await fixture.SeedWalletAsync(cart.CustomerId, 3161, 0m);
        await fixture.SeedActiveSubscriptionAsync(cart.CustomerId, 3161, 10m);
        var appPayment = await fixture.PrepareAppPaymentAsync(cart.CustomerCartId, 224.20m);

        var result = await fixture.CheckoutAsync(new CheckoutOrderCommand(
            cart.CartNumber, 7, 11, 41, null, null, appPayment.Token,
            [App(224.20m, appPayment.Reference)]));

        Assert.NotNull(result);
        Assert.Equal(22.42m, result.RewardEarned);
        fixture.ClearTracking();
        Assert.Equal(22.42m, await fixture.Db.CustomerWallets.AsNoTracking()
            .Where(wallet => wallet.WalletTypeId == 3161)
            .Select(wallet => wallet.CurrentBalance)
            .SingleAsync());
    }

    [Fact]
    public async Task Checkout_WithActiveCashbackConfiguration_CreditsCashbackWallet()
    {
        await using var fixture = await CheckoutFixture.CreateAsync();
        var cart = await fixture.SeedCartAsync("CART-CHECKOUT-CASHBACK");
        await fixture.SeedWalletAsync(cart.CustomerId, 3171, 0m, "CASHBACK");
        await fixture.SeedCashbackAsync(10m, 15m);
        var appPayment = await fixture.PrepareAppPaymentAsync(cart.CustomerCartId, 224.20m);

        var result = await fixture.CheckoutAsync(new CheckoutOrderCommand(
            cart.CartNumber, 7, 11, 41, null, null, appPayment.Token,
            [App(224.20m, appPayment.Reference)]));

        Assert.NotNull(result);
        Assert.Equal(15m, result.CashbackEarned);
        fixture.ClearTracking();
        Assert.Equal(15m, await fixture.Db.CustomerWallets.AsNoTracking()
            .Where(wallet => wallet.WalletTypeId == 3171)
            .Select(wallet => wallet.CurrentBalance)
            .SingleAsync());
    }

    [Fact]
    public async Task Checkout_WhenRepeatedWithDifferentState_IsRejected()
    {
        await using var fixture = await CheckoutFixture.CreateAsync();
        var cart = await fixture.SeedCartAsync("CART-CHECKOUT-RETRY-DIFFERENT");
        await fixture.CheckoutAsync(Command(cart.CartNumber, Cash(224.20m)));
        fixture.ClearTracking();

        var exception = await Assert.ThrowsAsync<DomainException>(() => fixture.CheckoutAsync(Command(
            cart.CartNumber,
            Upi(224.20m, "changed"))));

        Assert.Equal("The cart has already been checked out with different payment details.", exception.Message);
    }

    [Fact]
    public async Task Checkout_WhenCartIsEmpty_IsRejected()
    {
        await using var fixture = await CheckoutFixture.CreateAsync();
        var cart = await fixture.SeedCartAsync("CART-CHECKOUT-EMPTY", withItem: false);

        var exception = await Assert.ThrowsAsync<DomainException>(() =>
            fixture.CheckoutAsync(Command(cart.CartNumber, Cash(1m))));

        Assert.Equal("The cart must contain at least one item before checkout.", exception.Message);
    }

    [Fact]
    public async Task Checkout_WhenCartWasPaidWithoutOrder_IsRejected()
    {
        await using var fixture = await CheckoutFixture.CreateAsync();
        var cart = CheckoutPreviewServiceTests.CreateCart("CART-CHECKOUT-PAID", withItem: true);
        cart.ChangeStatus("PaymentPending");
        cart.ChangeStatus("Paid");
        fixture.Db.CustomerCarts.Add(cart);
        await fixture.Db.SaveChangesAsync();
        fixture.ClearTracking();

        var exception = await Assert.ThrowsAsync<DomainException>(() =>
            fixture.CheckoutAsync(Command(cart.CartNumber, Cash(224.20m))));

        Assert.Equal("Only an active cart can be checked out.", exception.Message);
    }

    [Fact]
    public async Task Checkout_WhenInvoiceGeneratorFails_ReturnsSuccessfulOrderAsPending()
    {
        await using var fixture = await CheckoutFixture.CreateAsync(invoiceFailure: true);
        var cart = await fixture.SeedCartAsync("CART-CHECKOUT-INVOICE-FAILURE");

        var result = await fixture.CheckoutAsync(Command(cart.CartNumber, Cash(224.20m)));

        Assert.NotNull(result);
        Assert.Equal("Pending", result.InvoiceStatus);
        fixture.ClearTracking();
        Assert.Equal(1, await fixture.Db.CustomerOrders.AsNoTracking().CountAsync());
        Assert.Equal("Paid", await fixture.Db.CustomerCarts.AsNoTracking().Select(item => item.CartStatus).SingleAsync());
    }

    [Fact]
    public async Task Checkout_ConcurrentAttempts_ProduceOneOrderAndOneIdempotentResponse()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"mart-checkout-{Guid.NewGuid():N}.db");
        CheckoutFixture? setup = null;
        CheckoutFixture? firstFixture = null;
        CheckoutFixture? secondFixture = null;
        try
        {
            setup = await CheckoutFixture.CreateAsync(databasePath: databasePath);
            var cart = await setup.SeedCartAsync("CART-CHECKOUT-CONCURRENT");
            setup.ClearTracking();

            firstFixture = await CheckoutFixture.CreateAsync(databasePath: databasePath, initialize: false);
            secondFixture = await CheckoutFixture.CreateAsync(databasePath: databasePath, initialize: false);
            var command = Command(cart.CartNumber, Cash(224.20m));

            var results = await Task.WhenAll(
                firstFixture.CheckoutAsync(command),
                secondFixture.CheckoutAsync(command));

            Assert.All(results, Assert.NotNull);
            Assert.Single(results.Select(result => result!.CustomerOrderId).Distinct());
            Assert.Single(results, result => result!.IsIdempotentRetry);
            setup.ClearTracking();
            Assert.Equal(1, await setup.Db.CustomerOrders.AsNoTracking().CountAsync());
            Assert.Equal(98m, await setup.Db.StoreStocks.AsNoTracking().Select(item => item.CurrentQuantity).SingleAsync());
            Assert.Equal(1, await setup.Db.StockMovements.AsNoTracking()
                .Where(item => item.MovementType == "SALE")
                .CountAsync());
        }
        finally
        {
            if (secondFixture is not null)
            {
                await secondFixture.DisposeAsync();
            }
            if (firstFixture is not null)
            {
                await firstFixture.DisposeAsync();
            }
            if (setup is not null)
            {
                await setup.DisposeAsync();
            }
            SqliteConnection.ClearAllPools();
            if (File.Exists(databasePath))
            {
                File.Delete(databasePath);
            }
        }
    }

    private static CheckoutOrderCommand Command(string cartNumber, params CheckoutPayment[] payments) =>
        new(cartNumber, 7, 11, 41, null, null, payments);

    private static CheckoutPayment Cash(decimal amount) => new("Cash", amount, null);
    private static CheckoutPayment Upi(decimal amount, string reference) => new("UPI", amount, reference);
    private static CheckoutPayment Card(decimal amount, string reference) => new("Card", amount, reference);
    private static CheckoutPayment App(decimal amount, string reference) => new("APP", amount, reference);

    private static Mart.Customer.Domain.Orders.CustomerOrder CreateSnapshotOrder()
    {
        var order = Mart.Customer.Domain.Orders.CustomerOrder.Create(
            1,
            "INV-SNAPSHOT-1",
            1,
            7,
            11,
            DateTime.UtcNow,
            1,
            110m,
            10m,
            18m,
            118m,
            null,
            0m,
            118m,
            41);
        order.AddItem(1, 501, "Cart product", 1m, 110m, 110m, 110m, 10m, 18m, 18m, 118m);
        return order;
    }
}

public sealed class OrderCheckoutEndpointTests : IClassFixture<CheckoutPreviewApiFactory>
{
    private readonly CheckoutPreviewApiFactory _factory;

    public OrderCheckoutEndpointTests(CheckoutPreviewApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Checkout_WhenRequestIsValid_UsesVersionedCommandEndpoint()
    {
        var cart = CheckoutPreviewServiceTests.CreateCart($"CART-ENDPOINT-{Guid.NewGuid():N}", withItem: true);
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.CustomerCarts.Add(cart);
            CheckoutFixture.AddInventory(db, initialStockQuantity: 100m);
            await db.SaveChangesAsync();
        }

        using var client = _factory.CreateClient();
        using var response = await client.PostAsJsonAsync(
            "/api/v1/orders/checkout",
            new
            {
                cartNumber = cart.CartNumber,
                payments = new[] { new { paymentMode = "Cash", amount = 224.20m } }
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<OrderCheckoutDto>();
        Assert.NotNull(result);
        Assert.Equal("Paid", result.OrderStatus);
    }
}

internal sealed class CheckoutFixture : IAsyncDisposable
{
    private readonly ServiceProvider _provider;
    private readonly AsyncServiceScope _scope;
    private readonly SqliteConnection _connection;

    private CheckoutFixture(ServiceProvider provider, AsyncServiceScope scope, SqliteConnection connection)
    {
        _provider = provider;
        _scope = scope;
        _connection = connection;
        Db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    }

    public ApplicationDbContext Db { get; }

    public static async Task<CheckoutFixture> CreateAsync(
        bool invoiceFailure = false,
        string? databasePath = null,
        bool initialize = true)
    {
        var services = new ServiceCollection();
        var connectionString = databasePath is null
            ? "Data Source=:memory:"
            : $"Data Source={databasePath};Default Timeout=10";
        var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();
        await using (var pragma = connection.CreateCommand())
        {
            pragma.CommandText = "PRAGMA foreign_keys = OFF;";
            await pragma.ExecuteNonQueryAsync();
        }
        services.AddSingleton(connection);
        services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connection));
        RegisterRepository<ICustomerCartRepository>(services, "CustomerCartRepository");
        RegisterRepository<ICustomerOrderRepository>(services, "CustomerOrderRepository");
        RegisterRepository<ICustomerWalletRepository>(services, "CustomerWalletRepository");
        RegisterRepository<IWalletBalanceBucketRepository>(services, "WalletBalanceBucketRepository");
        RegisterRepository<IWalletTransactionRepository>(services, "WalletTransactionRepository");
        RegisterRepository<IWalletTypeRepository>(services, "WalletTypeRepository");
        RegisterRepository<ICashbackConfigurationRepository>(services, "CashbackConfigurationRepository");
        RegisterRepository<ICustomerSubscriptionRepository>(services, "CustomerSubscriptionRepository");
        RegisterRepository<IUnitOfWork>(services, "UnitOfWork");
        services.AddScoped<IInventoryStockService, InventoryStockService>();
        services.AddLogging();
        services.AddApplication();
        services.RemoveAll<IInvoiceService>();
        services.AddSingleton<IInvoiceService>(new TestInvoiceService(invoiceFailure));

        var provider = services.BuildServiceProvider();
        var scope = provider.CreateAsyncScope();
        var fixture = new CheckoutFixture(provider, scope, connection);
        if (initialize)
        {
            await fixture.Db.Database.EnsureCreatedAsync();
        }
        return fixture;
    }

    public async Task<Mart.Customer.Domain.Carts.CustomerCart> SeedCartAsync(
        string cartNumber,
        bool withItem = true,
        decimal initialStockQuantity = 100m,
        bool isBatchApplicable = false,
        bool isExpiryApplicable = false,
        IReadOnlyList<BatchSeed>? batches = null)
    {
        var cart = CheckoutPreviewServiceTests.CreateCart(cartNumber, withItem);
        Db.CustomerCarts.Add(cart);
        if (withItem)
        {
            AddInventory(
                Db,
                initialStockQuantity,
                isBatchApplicable,
                isExpiryApplicable,
                batches);
        }
        await Db.SaveChangesAsync();
        ClearTracking();
        return cart;
    }

    internal static void AddInventory(
        ApplicationDbContext dbContext,
        decimal initialStockQuantity,
        bool isBatchApplicable = false,
        bool isExpiryApplicable = false,
        IReadOnlyList<BatchSeed>? batches = null)
    {
        var product = (Product)Activator.CreateInstance(typeof(Product), nonPublic: true)!;
        SetProperty(product, nameof(Product.ProductId), 101L);
        SetProperty(product, nameof(Product.ProductCode), "P-101");
        SetProperty(product, nameof(Product.ProductName), "Persisted product");
        SetProperty(product, nameof(Product.ProductType), "Grocery");
        SetProperty(product, nameof(Product.IsActive), true);
        SetProperty(product, nameof(Product.IsStockManaged), true);
        SetProperty(product, nameof(Product.IsBatchApplicable), isBatchApplicable);
        SetProperty(product, nameof(Product.IsExpiryApplicable), isExpiryApplicable);
        SetProperty(product, nameof(Product.DefaultPurchasePrice), 80m);
        SetProperty(product, nameof(Product.DefaultSellingPrice), 100m);
        SetProperty(product, nameof(Product.MRP), 110m);
        dbContext.Products.Add(product);
        dbContext.StoreStocks.Add(StoreStock.Create(
            7,
            11,
            101,
            initialStockQuantity,
            80m,
            100m,
            41,
            DateTime.UtcNow));

        foreach (var batch in batches ?? [])
        {
            dbContext.ProductBatchStocks.Add(ProductBatchStock.Create(
                7,
                11,
                101,
                batch.BatchNumber,
                batch.ManufacturingDate,
                batch.ExpiryDate,
                batch.Quantity,
                80m,
                100m,
                110m,
                41,
                DateTime.UtcNow));
        }
    }

    public async Task SeedWalletAsync(
        long customerId,
        int walletTypeId,
        decimal balance,
        string? walletTypeCode = null)
    {
        var wallet = await RedeemPreviewServiceTests.SeedWalletAsync(
            Db, customerId, walletTypeId, true, true, balance);
        if (balance > 0)
        {
            RedeemPreviewServiceTests.SeedBuckets(
                Db,
                wallet.CustomerWalletId,
                RedeemPreviewServiceTests.CreateBucket(
                    walletTypeId * 100L,
                    wallet.CustomerWalletId,
                    balance,
                    null,
                    DateTime.UtcNow));
        }
        await Db.SaveChangesAsync();
        if (!string.IsNullOrWhiteSpace(walletTypeCode))
        {
            var walletType = await Db.WalletTypes.SingleAsync(item => item.Id == walletTypeId);
            SetProperty(walletType, nameof(WalletType.Code), walletTypeCode);
            await Db.SaveChangesAsync();
        }
        ClearTracking();
    }

    public async Task SeedActiveSubscriptionAsync(
        long customerId,
        int walletTypeId,
        decimal extraPointPercentage)
    {
        var subscription = (CustomerSubscription)Activator.CreateInstance(
            typeof(CustomerSubscription),
            nonPublic: true)!;
        SetProperty(subscription, nameof(CustomerSubscription.CustomerId), customerId);
        SetProperty(subscription, nameof(CustomerSubscription.SubscriptionPlanId), 1);
        SetProperty(subscription, nameof(CustomerSubscription.SubscriptionAmount), 0m);
        SetProperty(subscription, nameof(CustomerSubscription.StartDate), DateTime.UtcNow.AddDays(-1));
        SetProperty(subscription, nameof(CustomerSubscription.ExpiryDate), DateTime.UtcNow.AddDays(30));
        SetProperty(subscription, nameof(CustomerSubscription.Status), "ACTIVE");
        SetProperty(subscription, nameof(CustomerSubscription.ExtraPointPercentage), extraPointPercentage);
        SetProperty(subscription, nameof(CustomerSubscription.FeeToWalletPercentage), 0m);
        SetProperty(subscription, nameof(CustomerSubscription.WalletTypeId), (int?)walletTypeId);
        SetProperty(subscription, nameof(CustomerSubscription.CreatedOn), DateTime.UtcNow);
        SetProperty(subscription, nameof(CustomerSubscription.CreatedBy), 41);
        Db.CustomerSubscriptions.Add(subscription);
        await Db.SaveChangesAsync();
        ClearTracking();
    }

    public async Task SeedCashbackAsync(decimal percentage, decimal maximumPerOrder)
    {
        var setting = (CashbackConfiguration)Activator.CreateInstance(
            typeof(CashbackConfiguration),
            nonPublic: true)!;
        SetProperty(setting, nameof(CashbackConfiguration.StoreId), (long?)11);
        SetProperty(setting, nameof(CashbackConfiguration.CashbackPercentage), (decimal?)percentage);
        SetProperty(setting, nameof(CashbackConfiguration.CashbackValidityDays), (int?)30);
        SetProperty(setting, nameof(CashbackConfiguration.MinimumPurchaseAmount), (decimal?)0m);
        SetProperty(setting, nameof(CashbackConfiguration.MaximumCashbackPerOrder), (decimal?)maximumPerOrder);
        SetProperty(setting, nameof(CashbackConfiguration.IsActive), (bool?)true);
        SetProperty(setting, nameof(CashbackConfiguration.StartDate), (DateTime?)DateTime.UtcNow.AddDays(-1));
        SetProperty(setting, nameof(CashbackConfiguration.EndDate), (DateTime?)DateTime.UtcNow.AddDays(1));
        Db.CashbackConfigurations.Add(setting);
        await Db.SaveChangesAsync();
        ClearTracking();
    }

    public Task<OrderCheckoutDto?> CheckoutAsync(CheckoutOrderCommand command) =>
        _scope.ServiceProvider.GetRequiredService<IOrderCheckoutService>()
            .CheckoutAsync(command, CancellationToken.None);

    public async Task<(string Token, string Reference)> PrepareAppPaymentAsync(long cartId, decimal amount)
    {
        var token = $"{cartId}.app-checkout-test";
        var reference = $"APP-{cartId}";
        var tokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
        var cart = await Db.CustomerCarts.SingleAsync(item => item.CustomerCartId == cartId);
        cart.BeginWalletPaymentAttempt(reference, tokenHash, amount, DateTime.UtcNow.AddMinutes(5));
        cart.UpdatePaymentStatus("PAID");
        await Db.SaveChangesAsync();
        ClearTracking();
        return (token, reference);
    }

    public void ClearTracking() => Db.ChangeTracker.Clear();

    public async ValueTask DisposeAsync()
    {
        await _scope.DisposeAsync();
        await _provider.DisposeAsync();
        await _connection.DisposeAsync();
    }

    private static void RegisterRepository<TService>(IServiceCollection services, string typeName)
        where TService : class
    {
        var implementation = typeof(ApplicationDbContext).Assembly.GetType(
            $"Mart.Customer.Persistence.Repositories.{typeName}",
            throwOnError: true)!;
        services.AddScoped(typeof(TService), implementation);
    }

    private static void SetProperty<TTarget, TValue>(
        TTarget target,
        string propertyName,
        TValue value)
        where TTarget : class =>
        typeof(TTarget).GetProperty(propertyName)!.SetValue(target, value);
}

internal sealed record BatchSeed(
    string BatchNumber,
    decimal Quantity,
    DateTime? ExpiryDate,
    DateTime? ManufacturingDate);

internal sealed class TestInvoiceService : IInvoiceService
{
    private readonly bool _fail;

    public TestInvoiceService(bool fail)
    {
        _fail = fail;
    }

    public Task<InvoiceArchiveResultDto> GenerateAndArchiveAsync(
        OrderInvoiceSnapshotDto invoice,
        CancellationToken cancellationToken = default)
    {
        if (_fail)
        {
            throw new IOException("Simulated invoice archive failure.");
        }

        return Task.FromResult(new InvoiceArchiveResultDto($"archive/{invoice.InvoiceNumber}.pdf"));
    }

    public Task<InvoiceDownloadResult> GetInvoiceAsync(
        long customerOrderId,
        OrderDetailAccessScope accessScope,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<InvoiceDownloadResult> GetOriginalInvoiceAsync(
        long customerOrderId,
        OrderDetailAccessScope accessScope,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
}
