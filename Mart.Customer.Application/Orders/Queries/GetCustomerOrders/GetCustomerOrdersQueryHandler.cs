using Mart.Customer.Application.Abstractions.Data;
using Mart.Customer.Application.Customers.Dtos;
using Mart.Customer.Application.Orders.Dtos;
using MediatR;

namespace Mart.Customer.Application.Orders.Queries.GetCustomerOrders;

public sealed class GetCustomerOrdersQueryHandler
    : IRequestHandler<GetCustomerOrdersQuery, PagedResultDto<OrderHistoryItemDto>>
{
    private readonly ICustomerOrderRepository _orderRepository;

    public GetCustomerOrdersQueryHandler(ICustomerOrderRepository orderRepository)
    {
        _orderRepository = orderRepository;
    }

    public async Task<PagedResultDto<OrderHistoryItemDto>> Handle(
        GetCustomerOrdersQuery request,
        CancellationToken cancellationToken)
    {
        var (orders, totalCount) = await _orderRepository.GetPagedByCustomerIdAsync(
            request.CustomerId,
            request.PageNumber,
            request.PageSize,
            cancellationToken);

        var totalPages = totalCount == 0
            ? 0
            : (int)Math.Ceiling(totalCount / (double)request.PageSize);

        return new PagedResultDto<OrderHistoryItemDto>(
            orders,
            request.PageNumber,
            request.PageSize,
            totalCount,
            totalPages);
    }
}
