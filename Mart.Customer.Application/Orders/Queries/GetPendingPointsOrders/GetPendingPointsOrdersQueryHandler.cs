using Mart.Customer.Application.Abstractions.Data;
using Mart.Customer.Application.Orders.Dtos;
using MediatR;

namespace Mart.Customer.Application.Orders.Queries.GetPendingPointsOrders;

public sealed class GetPendingPointsOrdersQueryHandler(ICustomerOrderRepository orderRepository)
    : IRequestHandler<GetPendingPointsOrdersQuery, IReadOnlyList<PendingPointsOrderDto>>
{
    public Task<IReadOnlyList<PendingPointsOrderDto>> Handle(
        GetPendingPointsOrdersQuery request,
        CancellationToken cancellationToken) =>
        orderRepository.GetPendingPointsAsync(cancellationToken);
}
