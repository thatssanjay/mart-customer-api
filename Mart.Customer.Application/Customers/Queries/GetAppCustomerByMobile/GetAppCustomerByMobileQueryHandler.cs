using Mart.Customer.Application.Abstractions.Data;
using Mart.Customer.Application.Customers.Dtos;
using MediatR;

namespace Mart.Customer.Application.Customers.Queries.GetAppCustomerByMobile;

public sealed class GetAppCustomerByMobileQueryHandler
    : IRequestHandler<GetAppCustomerByMobileQuery, AppCustomerProfileDto?>
{
    private readonly ICustomerRepository _customerRepository;

    public GetAppCustomerByMobileQueryHandler(ICustomerRepository customerRepository)
    {
        _customerRepository = customerRepository;
    }

    public async Task<AppCustomerProfileDto?> Handle(
        GetAppCustomerByMobileQuery request,
        CancellationToken cancellationToken)
    {
        var customer = await _customerRepository.GetByMobileNumberAsync(request.MobileNumber, cancellationToken);

        return customer is null
            ? null
            : new AppCustomerProfileDto(
                customer.CustomerId,
                customer.MobileNumber,
                customer.DisplayName,
                customer.Email,
                customer.AddressLine1);
    }
}
