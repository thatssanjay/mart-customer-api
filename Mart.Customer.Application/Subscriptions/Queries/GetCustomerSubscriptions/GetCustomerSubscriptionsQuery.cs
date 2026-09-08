using Mart.Customer.Application.Subscriptions.Dtos;
using MediatR;

namespace Mart.Customer.Application.Subscriptions.Queries.GetCustomerSubscriptions;

public sealed record GetCustomerSubscriptionsQuery(long CustomerId)
    : IRequest<IReadOnlyList<CustomerSubscriptionDto>>;
