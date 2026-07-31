using Mart.Customer.Application.Abstractions.Data;
using Mart.Customer.Application.Customers.Dtos;
using MediatR;

namespace Mart.Customer.Application.Customers.Queries.GetCustomers;

public sealed class GetCustomersQueryHandler
    : IRequestHandler<GetCustomersQuery, PagedResultDto<CustomerListItemDto>>
{
    private readonly ICustomerRepository _customerRepository;

    public GetCustomersQueryHandler(ICustomerRepository customerRepository)
    {
        _customerRepository = customerRepository;
    }

    public async Task<PagedResultDto<CustomerListItemDto>> Handle(
        GetCustomersQuery request,
        CancellationToken cancellationToken)
    {
        var (customers, totalCount) = await _customerRepository.GetPagedAsync(
            request.PageNumber,
            request.PageSize,
            request.DisplayName,
            request.MobileNumber,
            cancellationToken);

        var items = customers
            .Select(customer => new CustomerListItemDto(
                customer.CustomerId,
                customer.CustomerCode,
                customer.FirstName,
                customer.LastName,
                customer.MobileNumber,
                customer.Email,
                customer.Gender,
                customer.AddressLine1,
                customer.City,
                customer.IsActive))
            .ToList();

        var totalPages = totalCount == 0
            ? 0
            : (int)Math.Ceiling(totalCount / (double)request.PageSize);

        return new PagedResultDto<CustomerListItemDto>(
            items,
            request.PageNumber,
            request.PageSize,
            totalCount,
            totalPages);
    }
}
