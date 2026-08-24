using Mart.Customer.Application.Customers.Dtos;
using Mart.Customer.Application.Orders.Dtos;
using MediatR;

namespace Mart.Customer.Application.Orders.Queries.GetCustomerOrders;

public sealed record GetCustomerOrdersQuery(
    long CustomerId,
    int PageNumber = 1,
    int PageSize = 10) : IRequest<PagedResultDto<OrderHistoryItemDto>>;
