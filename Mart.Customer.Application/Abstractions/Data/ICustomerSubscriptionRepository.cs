using Mart.Customer.Domain.Subscriptions;

namespace Mart.Customer.Application.Abstractions.Data;

public interface ICustomerSubscriptionRepository
{
    Task<ActiveOrderRewardConfigurationDto?> GetActiveOrderRewardConfigurationAsync(
        long customerId,
        DateTime orderDate,
        CancellationToken cancellationToken = default);

    Task<SubscriptionPlan?> GetPlanByIdAsync(
        int subscriptionPlanId,
        CancellationToken cancellationToken = default);

    Task<CustomerSubscription?> GetByIdAndCustomerIdAsync(
        long id,
        long customerId,
        CancellationToken cancellationToken = default);

    Task AddAsync(CustomerSubscription subscription, CancellationToken cancellationToken = default);
}

public sealed record ActiveOrderRewardConfigurationDto(
    int WalletTypeId,
    decimal ExtraPointPercentage,
    DateTime ExpiryDate);
