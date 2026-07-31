using Mart.Customer.Application.Auth.Dtos;
using MediatR;

namespace Mart.Customer.Application.Auth.Commands.GenerateInternalUserToken;

public sealed record GenerateInternalUserTokenCommand(
    string UserId,
    string Password) : IRequest<AuthTokenDto>;
