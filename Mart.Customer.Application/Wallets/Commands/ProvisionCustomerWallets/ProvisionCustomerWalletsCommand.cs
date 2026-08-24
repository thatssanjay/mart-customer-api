using Mart.Customer.Application.Wallets.Dtos;
using MediatR;

namespace Mart.Customer.Application.Wallets.Commands.ProvisionCustomerWallets;

public sealed record ProvisionCustomerWalletsCommand(long CustomerId)
    : IRequest<ProvisionCustomerWalletsResultDto>;
