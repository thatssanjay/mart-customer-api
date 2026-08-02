using Mart.Customer.Application.Abstractions.Data;
using Mart.Customer.Application.Customers.Dtos;
using MediatR;

namespace Mart.Customer.Application.Customers.Queries.SearchCustomers;

public sealed class SearchCustomersQueryHandler
    : IRequestHandler<SearchCustomersQuery, IReadOnlyList<CustomerLookupDto>>
{
    private const int MaximumResults = 5;
    private readonly ICustomerRepository _customerRepository;

    public SearchCustomersQueryHandler(ICustomerRepository customerRepository)
    {
        _customerRepository = customerRepository;
    }

    public Task<IReadOnlyList<CustomerLookupDto>> Handle(
        SearchCustomersQuery request,
        CancellationToken cancellationToken)
    {
        return _customerRepository.SearchAsync(
            request.Search!,
            MaximumResults,
            cancellationToken);
    }
}
