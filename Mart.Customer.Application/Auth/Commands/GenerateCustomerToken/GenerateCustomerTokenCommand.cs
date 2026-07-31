using Mart.Customer.Application.Auth.Dtos;
using MediatR;

namespace Mart.Customer.Application.Auth.Commands.GenerateCustomerToken;

public sealed record GenerateCustomerTokenCommand(
    string MobileNumber,
    string Otp) : IRequest<AuthTokenDto>;
