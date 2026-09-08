using Mart.Customer.Application.Subscriptions.Dtos;
using MediatR;

namespace Mart.Customer.Application.Subscriptions.Queries.GetActiveSubscriptionPlans;

public sealed record GetActiveSubscriptionPlansQuery : IRequest<IReadOnlyList<SubscriptionPlanDto>>;
