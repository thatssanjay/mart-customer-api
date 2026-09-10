using Mart.Customer.Application.Orders.Dtos;
using MediatR;

namespace Mart.Customer.Application.Orders.Queries.GetPendingPointsOrders;

public sealed record GetPendingPointsOrdersQuery
    : IRequest<IReadOnlyList<PendingPointsOrderDto>>;
