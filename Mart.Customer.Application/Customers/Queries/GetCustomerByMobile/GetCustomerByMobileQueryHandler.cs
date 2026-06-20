using Mart.Customer.Application.Abstractions.Data;
using Mart.Customer.Application.Customers.Dtos;
using MediatR;

namespace Mart.Customer.Application.Customers.Queries.GetCustomerByMobile;

public sealed class GetCustomerByMobileQueryHandler : IRequestHandler<GetCustomerByMobileQuery, CustomerContactDto?>
{
    private readonly ICustomerRepository _customerRepository;

    public GetCustomerByMobileQueryHandler(ICustomerRepository customerRepository)
    {
        _customerRepository = customerRepository;
    }

    public async Task<CustomerContactDto?> Handle(GetCustomerByMobileQuery request, CancellationToken cancellationToken)
    {
        var customer = await _customerRepository.GetByMobileNumberAsync(request.MobileNumber, cancellationToken);

        return customer is null
            ? null
            : new CustomerContactDto(customer.MobileNumber, customer.Email, customer.FirstName, customer.LastName);
    }
}
