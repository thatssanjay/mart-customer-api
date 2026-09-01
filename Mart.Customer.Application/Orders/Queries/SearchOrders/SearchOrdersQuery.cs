using Mart.Customer.Application.Customers.Dtos;
using Mart.Customer.Application.Orders.Dtos;
using MediatR;

namespace Mart.Customer.Application.Orders.Queries.SearchOrders;

public sealed record SearchOrdersQuery(
    long FranchiseId,
    long MartStoreId,
    string? CustomerName = null,
    string? MobileNumber = null,
    string? InvoiceNumber = null,
    DateTime? FromDate = null,
    DateTime? ToDate = null,
    int PageNumber = 1,
    int PageSize = 10) : IRequest<PagedResultDto<OrderSearchItemDto>>;
