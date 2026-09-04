using Mart.Customer.Application.Wallets.Dtos;
using MediatR;

namespace Mart.Customer.Application.Wallets.Queries.GetCustomerWalletBalances;

public sealed record GetCustomerWalletBalancesQuery(long CustomerId)
    : IRequest<CustomerWalletBalancesDto?>;
