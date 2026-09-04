using Mart.Customer.Application.Abstractions.Data;
using Mart.Customer.Application.Wallets.Dtos;
using MediatR;

namespace Mart.Customer.Application.Wallets.Queries.GetCustomerWalletBalances;

public sealed class GetCustomerWalletBalancesQueryHandler(
    ICustomerRepository customerRepository,
    ICustomerWalletRepository customerWalletRepository)
    : IRequestHandler<GetCustomerWalletBalancesQuery, CustomerWalletBalancesDto?>
{
    public async Task<CustomerWalletBalancesDto?> Handle(
        GetCustomerWalletBalancesQuery request,
        CancellationToken cancellationToken)
    {
        if (!await customerRepository.ExistsByIdAsync(request.CustomerId, cancellationToken))
        {
            return null;
        }

        var wallets = await customerWalletRepository.GetBalancesByCustomerIdAsync(
            request.CustomerId, cancellationToken);
        return new CustomerWalletBalancesDto(request.CustomerId, wallets);
    }
}
