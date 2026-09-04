using FluentValidation;
using Mart.Customer.Application.Common.Behaviors;
using Mart.Customer.Application.Orders.Services;
using Mart.Customer.Application.Payments.Services;
using Mart.Customer.Application.Wallets.Services;
using Mart.Customer.Application.Wallets.Engine;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Mart.Customer.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        services.AddMediatR(configuration =>
        {
            configuration.RegisterServicesFromAssembly(assembly);
        });

        services.AddValidatorsFromAssembly(assembly);
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddScoped<IWalletRedemptionPreviewService, WalletRedemptionPreviewService>();
        services.AddScoped<IOrderCheckoutCalculator, OrderCheckoutCalculator>();
        services.AddScoped<IOrderCheckoutService, OrderCheckoutService>();
        services.AddScoped<IWalletPaymentRequestService, WalletPaymentRequestService>();
        services.AddScoped<ICustomerWalletResolver, CustomerWalletResolver>();
        services.AddScoped<IWalletLedgerService, WalletLedgerService>();
        services.AddScoped<IWalletEngineService, WalletEngineService>();

        return services;
    }
}
