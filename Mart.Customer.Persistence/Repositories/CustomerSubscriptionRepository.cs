using Mart.Customer.Application.Abstractions.Data;
using Mart.Customer.Application.Subscriptions.Dtos;
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

    public async Task<IReadOnlyList<SubscriptionPlanDto>> GetActivePlansAsync(
        DateTime currentDate,
        CancellationToken cancellationToken = default)
    {
        var effectiveDate = currentDate.Date;
        return await _dbContext.SubscriptionPlans
            .AsNoTracking()
            .Where(plan => plan.IsActive &&
                plan.EffectiveFrom <= effectiveDate &&
                plan.EffectiveTo >= effectiveDate)
            .OrderBy(plan => plan.SubscriptionId)
            .Select(plan => new SubscriptionPlanDto(
                plan.SubscriptionId,
                plan.PlanName,
                plan.SubscriptionFee,
                plan.DurationValue,
                plan.DurationType,
                plan.ExtraPointPercentage,
                plan.FeeToWalletPercentage,
                plan.WalletTypeId,
                DateOnly.FromDateTime(plan.EffectiveFrom),
                DateOnly.FromDateTime(plan.EffectiveTo)))
            .ToListAsync(cancellationToken);
    }

    public Task<SubscriptionPlan?> GetPlanByIdAsync(
        int subscriptionPlanId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.SubscriptionPlans
            .AsNoTracking()
            .SingleOrDefaultAsync(plan => plan.SubscriptionId == subscriptionPlanId, cancellationToken);
    }

    public Task<bool> HasActiveSubscriptionAsync(
        long customerId,
        int subscriptionPlanId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.CustomerSubscriptions.AsNoTracking().AnyAsync(
            subscription => subscription.CustomerId == customerId &&
                subscription.SubscriptionPlanId == subscriptionPlanId &&
                subscription.Status == "ACTIVE", cancellationToken);
    }

    public async Task<IReadOnlyList<CustomerSubscriptionDto>> GetCustomerSubscriptionsAsync(
        long customerId,
        CancellationToken cancellationToken = default)
    {
        return await (
            from subscription in _dbContext.CustomerSubscriptions.AsNoTracking()
            join plan in _dbContext.SubscriptionPlans.AsNoTracking()
                on subscription.SubscriptionPlanId equals plan.SubscriptionId
            where subscription.CustomerId == customerId
            orderby subscription.CreatedOn descending, subscription.Id descending
            select new CustomerSubscriptionDto(
                subscription.Id, subscription.SubscriptionPlanId, plan.PlanName,
                subscription.SubscriptionAmount, subscription.StartDate, subscription.ExpiryDate,
                subscription.Status, subscription.ExtraPointPercentage, subscription.FeeToWalletPercentage,
                subscription.WalletTypeId, subscription.WalletCreditAmount, subscription.IsPointCreated,
                subscription.PointReferenceId, subscription.PaymentTransactionId, subscription.CreatedOn,
                new SubscriptionPlanDto(
                    plan.SubscriptionId, plan.PlanName, plan.SubscriptionFee,
                    plan.DurationValue, plan.DurationType, plan.ExtraPointPercentage,
                    plan.FeeToWalletPercentage, plan.WalletTypeId,
                    DateOnly.FromDateTime(plan.EffectiveFrom), DateOnly.FromDateTime(plan.EffectiveTo))))
            .ToListAsync(cancellationToken);
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
