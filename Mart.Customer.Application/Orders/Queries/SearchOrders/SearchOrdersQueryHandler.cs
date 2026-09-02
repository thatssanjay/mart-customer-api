using Mart.Customer.Application.Abstractions.Data;
using Mart.Customer.Application.Customers.Dtos;
using Mart.Customer.Application.Orders.Dtos;
using MediatR;

namespace Mart.Customer.Application.Orders.Queries.SearchOrders;

public sealed class SearchOrdersQueryHandler
    : IRequestHandler<SearchOrdersQuery, PagedResultDto<OrderSearchItemDto>>
{
    private readonly ICustomerOrderRepository _orderRepository;

    public SearchOrdersQueryHandler(ICustomerOrderRepository orderRepository)
    {
        _orderRepository = orderRepository;
    }

    public async Task<PagedResultDto<OrderSearchItemDto>> Handle(
        SearchOrdersQuery request,
        CancellationToken cancellationToken)
    {
        var (orders, totalCount) = await _orderRepository.SearchPagedAsync(
            new OrderSearchCriteria(
                request.FranchiseId,
                request.MartStoreId,
                request.CustomerName,
                request.MobileNumber,
                request.InvoiceNumber,
                request.PageNumber,
                request.PageSize),
            cancellationToken);

        var totalPages = totalCount == 0
            ? 0
            : (int)Math.Ceiling(totalCount / (double)request.PageSize);

        return new PagedResultDto<OrderSearchItemDto>(
            orders,
            request.PageNumber,
            request.PageSize,
            totalCount,
            totalPages);
    }
}
