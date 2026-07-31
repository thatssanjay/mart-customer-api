using Mapster;
using Mart.Customer.Application.Abstractions.Data;
using Mart.Customer.Application.Customers.Dtos;
using Mart.Customer.Domain.Common;
using MediatR;

namespace Mart.Customer.Application.Customers.Commands.UpdateCustomer;

public sealed class UpdateCustomerCommandHandler
    : IRequestHandler<UpdateCustomerCommand, CustomerDto?>
{
    private readonly ICustomerRepository _customerRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateCustomerCommandHandler(
        ICustomerRepository customerRepository,
        IUnitOfWork unitOfWork)
    {
        _customerRepository = customerRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<CustomerDto?> Handle(
        UpdateCustomerCommand request,
        CancellationToken cancellationToken)
    {
        var customer = await _customerRepository.GetByIdAsync(request.CustomerId, cancellationToken);
        if (customer is null)
        {
            return null;
        }

        var mobileExists = await _customerRepository.ExistsByMobileNumberAsync(
            request.MobileNumber,
            request.CustomerId,
            cancellationToken);

        if (mobileExists)
        {
            throw new DomainException("Duplicate mobile number.");
        }

        customer.UpdateDetails(
            request.DisplayName!,
            request.MobileNumber,
            request.Email,
            request.AddressLine1,
            DateTime.UtcNow);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return customer.Adapt<CustomerDto>();
    }
}
