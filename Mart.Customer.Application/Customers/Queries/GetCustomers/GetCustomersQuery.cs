using Mart.Customer.Application.Customers.Dtos;
using MediatR;

namespace Mart.Customer.Application.Customers.Queries.GetCustomers;

public sealed record GetCustomersQuery(
    int PageNumber = 1,
    int PageSize = 10,
    string? DisplayName = null,
    string? MobileNumber = null) : IRequest<PagedResultDto<CustomerListItemDto>>;
