using Mart.Customer.Application.Abstractions.Auth;
using Mart.Customer.Application.Abstractions.Data;
using Mart.Customer.Application.Auth.Dtos;
using Mart.Customer.Domain.Common;
using MediatR;

namespace Mart.Customer.Application.Auth.Commands.GenerateCustomerToken;

public sealed class GenerateCustomerTokenCommandHandler : IRequestHandler<GenerateCustomerTokenCommand, AuthTokenDto>
{
    private readonly ICustomerRepository _customerRepository;
    private readonly IOtpValidator _otpValidator;
    private readonly IJwtTokenService _jwtTokenService;

    public GenerateCustomerTokenCommandHandler(
        ICustomerRepository customerRepository,
        IOtpValidator otpValidator,
        IJwtTokenService jwtTokenService)
    {
        _customerRepository = customerRepository;
        _otpValidator = otpValidator;
        _jwtTokenService = jwtTokenService;
    }

    public async Task<AuthTokenDto> Handle(GenerateCustomerTokenCommand request, CancellationToken cancellationToken)
    {
        var customer = await _customerRepository.GetByMobileNumberAsync(request.MobileNumber, cancellationToken);
        if (customer is null)
        {
            throw new DomainException("Customer mobile number was not found.");
        }

        var isOtpValid = await _otpValidator.IsValidAsync(request.MobileNumber, request.Otp, cancellationToken);
        if (!isOtpValid)
        {
            throw new DomainException("Invalid OTP.");
        }

        return _jwtTokenService.GenerateToken(
            new TokenSubject(
                customer.CustomerId.ToString(),
                customer.DisplayName ?? customer.FirstName,
                "Customer",
                "customer",
                new Dictionary<string, string>
                {
                    ["mobileNumber"] = customer.MobileNumber
                }));
    }
}
