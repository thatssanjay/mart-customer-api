using Mart.Customer.Application.Abstractions.Data;
using Mart.Customer.Application.Customers.Dtos;
using MediatR;

namespace Mart.Customer.Application.Customers.Commands.UpdateAppCustomerProfile;

public sealed class UpdateAppCustomerProfileCommandHandler
    : IRequestHandler<UpdateAppCustomerProfileCommand, AppCustomerProfileDto?>
{
    private readonly ICustomerRepository _customerRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateAppCustomerProfileCommandHandler(
        ICustomerRepository customerRepository,
        IUnitOfWork unitOfWork)
    {
        _customerRepository = customerRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<AppCustomerProfileDto?> Handle(
        UpdateAppCustomerProfileCommand request,
        CancellationToken cancellationToken)
    {
        var customer = await _customerRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (customer is null)
        {
            return null;
        }

        customer.UpdateProfile(request.DisplayName!, request.EmailAddress, request.Address, DateTime.UtcNow);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new AppCustomerProfileDto(
            customer.CustomerId,
            customer.MobileNumber,
            customer.DisplayName,
            customer.Email,
            customer.AddressLine1);
    }
}
