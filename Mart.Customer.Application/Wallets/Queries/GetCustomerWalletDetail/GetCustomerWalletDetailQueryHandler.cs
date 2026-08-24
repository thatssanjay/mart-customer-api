using Mart.Customer.Application.Abstractions.Data;
using Mart.Customer.Application.Wallets.Dtos;
using MediatR;

namespace Mart.Customer.Application.Wallets.Queries.GetCustomerWalletDetail;

public sealed class GetCustomerWalletDetailQueryHandler
    : IRequestHandler<GetCustomerWalletDetailQuery, CustomerWalletDto?>
{
    private readonly ICustomerWalletRepository _customerWalletRepository;

    public GetCustomerWalletDetailQueryHandler(ICustomerWalletRepository customerWalletRepository)
    {
        _customerWalletRepository = customerWalletRepository;
    }

    public Task<CustomerWalletDto?> Handle(
        GetCustomerWalletDetailQuery request,
        CancellationToken cancellationToken)
    {
        return _customerWalletRepository.GetDetailAsync(
            request.CustomerId,
            request.WalletTypeId,
            cancellationToken);
    }
}
