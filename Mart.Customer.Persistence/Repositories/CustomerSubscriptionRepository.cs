using Mart.Customer.Application.Abstractions.Data;
using Mart.Customer.Domain.Subscriptions;
using Microsoft.EntityFrameworkCore;

namespace Mart.Customer.Persistence.Repositories;

internal sealed class CustomerSubscriptionRepository : ICustomerSubscriptionRepository
{
    private readonly ApplicationDbContext _dbContext;

    public CustomerSubscriptionRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<SubscriptionPlan?> GetPlanByIdAsync(
        int subscriptionPlanId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.SubscriptionPlans
            .AsNoTracking()
            .SingleOrDefaultAsync(plan => plan.SubscriptionId == subscriptionPlanId, cancellationToken);
    }

    public Task<ActiveOrderRewardConfigurationDto?> GetActiveOrderRewardConfigurationAsync(
        long customerId,
        DateTime orderDate,
        CancellationToken cancellationToken = default)
    {
        var effectiveDate = orderDate.Date;
        return _dbContext.CustomerSubscriptions
            .AsNoTracking()
            .Where(subscription =>
                subscription.CustomerId == customerId &&
                subscription.Status == "ACTIVE" &&
                subscription.StartDate <= effectiveDate &&
                subscription.ExpiryDate >= effectiveDate &&
                subscription.WalletTypeId.HasValue &&
                subscription.ExtraPointPercentage > 0)
            .OrderByDescending(subscription => subscription.StartDate)
            .ThenByDescending(subscription => subscription.Id)
            .Select(subscription => new ActiveOrderRewardConfigurationDto(
                subscription.WalletTypeId!.Value,
                subscription.ExtraPointPercentage,
                subscription.ExpiryDate))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<CustomerSubscription?> GetByIdAndCustomerIdAsync(
        long id,
        long customerId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.CustomerSubscriptions
            .SingleOrDefaultAsync(
                subscription => subscription.Id == id && subscription.CustomerId == customerId,
                cancellationToken);
    }

    public Task AddAsync(CustomerSubscription subscription, CancellationToken cancellationToken = default)
    {
        return _dbContext.CustomerSubscriptions.AddAsync(subscription, cancellationToken).AsTask();
    }
}
