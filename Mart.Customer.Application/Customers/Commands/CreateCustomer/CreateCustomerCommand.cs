using Mart.Customer.Application.Customers.Dtos;
using MediatR;

namespace Mart.Customer.Application.Customers.Commands.CreateCustomer;

public sealed record CreateCustomerCommand(
    string? CustomerCode,
    string? FirstName,
    string? LastName,
    string? DisplayName,
    string MobileNumber,
    string? Email,
    string? Gender,
    DateTime? DateOfBirth,
    string? AddressLine1,
    string? AddressLine2,
    string? City,
    string? State,
    string? Country,
    string? PinCode,
    string? PreferredLanguage,
    string? RegistrationSource,
    bool IsMobileVerified,
    bool IsEmailVerified,
    bool IsActive,
    bool IsBlocked,
    DateTime? LastLoginOn,
    DateTime? CreatedOn,
    DateTime? ModifiedOn) : IRequest<CustomerDto>;
