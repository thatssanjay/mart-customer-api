using Mart.Customer.Application.Abstractions.Data;
using Mart.Customer.Persistence.Repositories;
using Mart.Customer.Application.Promotions;
using Mart.Customer.Persistence.Services;
using Mart.Customer.Application.Inventory.Services;
using Mart.Customer.Application.Referrals.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Mart.Customer.Persistence;

public static class DependencyInjection
{
    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' was not found.");

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(connectionString));

        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<ICashbackConfigurationRepository, CashbackConfigurationRepository>();
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IStockMovementRepository, StockMovementRepository>();
        services.AddScoped<ICustomerCartRepository, CustomerCartRepository>();
        services.AddScoped<ICustomerOrderRepository, CustomerOrderRepository>();
        services.AddScoped<ICustomerOrderInvoiceRepository, CustomerOrderInvoiceRepository>();
        services.AddScoped<IStorePromotionService, StorePromotionService>();
        services.AddScoped<IInternalUserRepository, InternalUserRepository>();
        services.AddScoped<ICustomerSubscriptionRepository, CustomerSubscriptionRepository>();
        services.AddScoped<ICustomerReferralRepository, CustomerReferralRepository>();
        services.AddScoped<IWalletTypeRepository, WalletTypeRepository>();
        services.AddScoped<ICustomerWalletRepository, CustomerWalletRepository>();
        services.AddScoped<IWalletTransactionRepository, WalletTransactionRepository>();
        services.AddScoped<IWalletTopUpPaymentRepository, WalletTopUpPaymentRepository>();
        services.AddScoped<IWalletBalanceBucketRepository, WalletBalanceBucketRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IWalletOperationRepository, WalletOperationRepository>();
        services.AddScoped<IWalletPostingGuard, WalletPostingGuard>();
        services.AddScoped<IOrderWalletSourceRepository, OrderWalletSourceRepository>();
        services.AddScoped<IInventoryStockService, InventoryStockService>();
        services.AddScoped<IReferralRewardService, ReferralRewardService>();

        return services;
    }
}
