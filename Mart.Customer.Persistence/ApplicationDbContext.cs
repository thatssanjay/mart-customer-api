using Microsoft.EntityFrameworkCore;
using Mart.Customer.Persistence.Auth;
using Mart.Customer.Domain.Cashback;
using Mart.Customer.Domain.Inventory;
using Mart.Customer.Domain.Carts;
using Mart.Customer.Domain.Subscriptions;
using Mart.Customer.Domain.Wallets;
using Mart.Customer.Domain.Orders;
using CustomerEntity = Mart.Customer.Domain.Customers.Customer;
using Mart.Customer.Persistence.MasterData;

namespace Mart.Customer.Persistence;

public sealed class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<CustomerEntity> Customers => Set<CustomerEntity>();

    public DbSet<CashbackConfiguration> CashbackConfigurations => Set<CashbackConfiguration>();

    public DbSet<Product> Products => Set<Product>();

    public DbSet<StoreStock> StoreStocks => Set<StoreStock>();

    public DbSet<ProductBatchStock> ProductBatchStocks => Set<ProductBatchStock>();

    public DbSet<StockMovement> StockMovements => Set<StockMovement>();

    public DbSet<StockAdjustment> StockAdjustments => Set<StockAdjustment>();

    public DbSet<CustomerCart> CustomerCarts => Set<CustomerCart>();

    public DbSet<CustomerCartItem> CustomerCartItems => Set<CustomerCartItem>();

    public DbSet<CustomerOrder> CustomerOrders => Set<CustomerOrder>();

    public DbSet<CustomerOrderItem> CustomerOrderItems => Set<CustomerOrderItem>();

    public DbSet<CustomerOrderPayment> CustomerOrderPayments => Set<CustomerOrderPayment>();

    public DbSet<CustomerOrderInvoiceDocument> CustomerOrderInvoiceDocuments => Set<CustomerOrderInvoiceDocument>();

    public DbSet<MartStoreEntity> MartStores => Set<MartStoreEntity>();

    public DbSet<UserMasterEntity> Users => Set<UserMasterEntity>();

    public DbSet<UserRoleEntity> UserRoles => Set<UserRoleEntity>();

    public DbSet<RoleMasterEntity> Roles => Set<RoleMasterEntity>();

    public DbSet<UserMartAccessEntity> UserMartAccesses => Set<UserMartAccessEntity>();

    public DbSet<SubscriptionPlan> SubscriptionPlans => Set<SubscriptionPlan>();

    public DbSet<CustomerSubscription> CustomerSubscriptions => Set<CustomerSubscription>();

    public DbSet<WalletType> WalletTypes => Set<WalletType>();

    public DbSet<CustomerWallet> CustomerWallets => Set<CustomerWallet>();

    public DbSet<WalletTransaction> WalletTransactions => Set<WalletTransaction>();

    public DbSet<WalletBalanceBucket> WalletBalanceBuckets => Set<WalletBalanceBucket>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}
