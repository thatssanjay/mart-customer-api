using Mart.Customer.Application.Abstractions.Data;
using Mart.Customer.Application.Subscriptions.Dtos;
using MediatR;

namespace Mart.Customer.Application.Subscriptions.Queries.GetActiveSubscriptionPlans;

public sealed class GetActiveSubscriptionPlansQueryHandler
    : IRequestHandler<GetActiveSubscriptionPlansQuery, IReadOnlyList<SubscriptionPlanDto>>
{
    private readonly ICustomerSubscriptionRepository _subscriptionRepository;

    public GetActiveSubscriptionPlansQueryHandler(ICustomerSubscriptionRepository subscriptionRepository)
    {
        _subscriptionRepository = subscriptionRepository;
    }

    public Task<IReadOnlyList<SubscriptionPlanDto>> Handle(
        GetActiveSubscriptionPlansQuery request,
        CancellationToken cancellationToken)
    {
        return _subscriptionRepository.GetActivePlansAsync(DateTime.UtcNow.Date, cancellationToken);
    }
}
