using Mapster;
using Mart.Customer.Application.Abstractions.Data;
using Mart.Customer.Application.Customers.Dtos;
using Mart.Customer.Application.Common.Utilities;
using Mart.Customer.Domain.Common;
using MediatR;
using CustomerEntity = Mart.Customer.Domain.Customers.Customer;

namespace Mart.Customer.Application.Customers.Commands.CreateCustomer;

public sealed class CreateCustomerCommandHandler : IRequestHandler<CreateCustomerCommand, CustomerDto>
{
    private readonly ICustomerRepository _customerRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateCustomerCommandHandler(ICustomerRepository customerRepository, IUnitOfWork unitOfWork)
    {
        _customerRepository = customerRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<CustomerDto> Handle(CreateCustomerCommand request, CancellationToken cancellationToken)
    {
        var mobileExists = await _customerRepository.ExistsByMobileNumberAsync(request.MobileNumber, cancellationToken);
        if (mobileExists)
        {
            throw new DomainException("Duplicate customer.");
        }

        var customerCode = await GenerateUniqueCustomerCodeAsync(
            request.DisplayName!,
            request.MobileNumber,
            cancellationToken);

        var customer = CustomerEntity.Create(
            customerCode,
            request.FirstName,
            request.LastName,
            request.DisplayName,
            request.MobileNumber,
            request.Email,
            request.Gender,
            request.DateOfBirth,
            request.AddressLine1,
            request.AddressLine2,
            request.City,
            request.State,
            request.Country,
            request.PinCode,
            request.PreferredLanguage,
            request.RegistrationSource,
            request.IsMobileVerified,
            request.IsEmailVerified,
            request.IsActive,
            request.IsBlocked,
            request.LastLoginOn,
            DateTime.UtcNow,
            request.ModifiedOn);

        await _customerRepository.AddAsync(customer, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return customer.Adapt<CustomerDto>();
    }

    private async Task<string> GenerateUniqueCustomerCodeAsync(
        string displayName,
        string mobileNumber,
        CancellationToken cancellationToken)
    {
        const int maximumAttempts = 10;

        for (var attempt = 0; attempt < maximumAttempts; attempt++)
        {
            var code = ReferenceCodeGenerator.Generate(displayName, mobileNumber);
            if (!await _customerRepository.ExistsByCustomerCodeAsync(code, cancellationToken))
            {
                return code;
            }
        }

        throw new DomainException("Unable to generate a unique customer code.");
    }
}
