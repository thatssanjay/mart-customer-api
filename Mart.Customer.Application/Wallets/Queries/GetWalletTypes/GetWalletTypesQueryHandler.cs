using Mart.Customer.Application.Abstractions.Data;
using Mart.Customer.Application.Wallets.Dtos;
using MediatR;

namespace Mart.Customer.Application.Wallets.Queries.GetWalletTypes;

public sealed class GetWalletTypesQueryHandler
    : IRequestHandler<GetWalletTypesQuery, IReadOnlyList<WalletTypeDto>>
{
    private readonly IWalletTypeRepository _walletTypeRepository;

    public GetWalletTypesQueryHandler(IWalletTypeRepository walletTypeRepository)
    {
        _walletTypeRepository = walletTypeRepository;
    }

    public Task<IReadOnlyList<WalletTypeDto>> Handle(
        GetWalletTypesQuery request,
        CancellationToken cancellationToken)
    {
        return _walletTypeRepository.GetAsync(request.ActiveOnly, cancellationToken);
    }
}
