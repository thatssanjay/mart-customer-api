using Mart.Customer.Application.Abstractions.Data;
using Mart.Customer.Application.Wallets.Dtos;
using MediatR;

namespace Mart.Customer.Application.Wallets.Queries.GetCustomerWallets;

public sealed class GetCustomerWalletsQueryHandler
    : IRequestHandler<GetCustomerWalletsQuery, IReadOnlyList<CustomerWalletDto>>
{
    private readonly ICustomerWalletRepository _customerWalletRepository;

    public GetCustomerWalletsQueryHandler(ICustomerWalletRepository customerWalletRepository)
    {
        _customerWalletRepository = customerWalletRepository;
    }

    public Task<IReadOnlyList<CustomerWalletDto>> Handle(
        GetCustomerWalletsQuery request,
        CancellationToken cancellationToken)
    {
        return _customerWalletRepository.GetDetailsByCustomerIdAsync(
            request.CustomerId,
            request.IncludeInactive,
            cancellationToken);
    }
}
