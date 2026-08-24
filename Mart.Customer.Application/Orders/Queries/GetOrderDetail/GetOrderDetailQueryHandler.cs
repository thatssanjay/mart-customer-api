using Mart.Customer.Application.Abstractions.Data;
using Mart.Customer.Application.Orders.Dtos;
using MediatR;

namespace Mart.Customer.Application.Orders.Queries.GetOrderDetail;

public sealed class GetOrderDetailQueryHandler
    : IRequestHandler<GetOrderDetailQuery, OrderDetailResult>
{
    private readonly ICustomerOrderRepository _orderRepository;

    public GetOrderDetailQueryHandler(ICustomerOrderRepository orderRepository)
    {
        _orderRepository = orderRepository;
    }

    public Task<OrderDetailResult> Handle(
        GetOrderDetailQuery request,
        CancellationToken cancellationToken) =>
        _orderRepository.GetDetailAsync(
            request.CustomerOrderId,
            request.AccessScope,
            cancellationToken);
}
