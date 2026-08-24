using Mart.Customer.Application.Wallets.Dtos;
using MediatR;

namespace Mart.Customer.Application.Wallets.Queries.GetCustomerWallets;

public sealed record GetCustomerWalletsQuery(long CustomerId, bool IncludeInactive)
    : IRequest<IReadOnlyList<CustomerWalletDto>>;
