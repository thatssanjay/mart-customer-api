using Mart.Customer.Application.Abstractions.Data;
using Mart.Customer.Application.Subscriptions.Dtos;
using Mart.Customer.Domain.Common;
using MediatR;

namespace Mart.Customer.Application.Subscriptions.Queries.GetCustomerSubscriptions;

public sealed class GetCustomerSubscriptionsQueryHandler(
    ICustomerRepository customers,
    ICustomerSubscriptionRepository subscriptions)
    : IRequestHandler<GetCustomerSubscriptionsQuery, IReadOnlyList<CustomerSubscriptionDto>>
{
    public async Task<IReadOnlyList<CustomerSubscriptionDto>> Handle(
        GetCustomerSubscriptionsQuery request,
        CancellationToken cancellationToken)
    {
        if (!await customers.ExistsByIdAsync(request.CustomerId, cancellationToken))
        {
            throw new DomainException("Customer not found.");
        }

        return await subscriptions.GetCustomerSubscriptionsAsync(request.CustomerId, cancellationToken);
    }
}
