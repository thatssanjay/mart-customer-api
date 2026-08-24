using Mart.Customer.Application.Abstractions.Data;
using Mart.Customer.Persistence.Repositories;
using Mart.Customer.Persistence.Services;
using Mart.Customer.Application.Inventory.Services;
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
        services.AddScoped<IInternalUserRepository, InternalUserRepository>();
        services.AddScoped<ICustomerSubscriptionRepository, CustomerSubscriptionRepository>();
        services.AddScoped<IWalletTypeRepository, WalletTypeRepository>();
        services.AddScoped<ICustomerWalletRepository, CustomerWalletRepository>();
        services.AddScoped<IWalletTransactionRepository, WalletTransactionRepository>();
        services.AddScoped<IWalletBalanceBucketRepository, WalletBalanceBucketRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IInventoryStockService, InventoryStockService>();

        return services;
    }
}
