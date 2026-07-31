using Mart.Customer.Application.Customers.Dtos;
using MediatR;

namespace Mart.Customer.Application.Customers.Commands.UpdateCustomer;

public sealed record UpdateCustomerCommand(
    long CustomerId,
    string? DisplayName,
    string MobileNumber,
    string? Email,
    string? AddressLine1) : IRequest<CustomerDto?>;
