using Mart.Customer.Application.Customers.Dtos;
using MediatR;

namespace Mart.Customer.Application.Customers.Commands.UpdateAppCustomerProfile;

public sealed record UpdateAppCustomerProfileCommand(
    long UserId,
    string? DisplayName,
    string? EmailAddress,
    string? Address) : IRequest<AppCustomerProfileDto?>;
