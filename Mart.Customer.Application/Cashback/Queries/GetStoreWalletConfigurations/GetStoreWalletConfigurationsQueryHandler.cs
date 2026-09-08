using Mart.Customer.Application.Abstractions.Data;
using Mart.Customer.Application.Cashback.Dtos;
using Mart.Customer.Domain.Wallets;
using MediatR;

namespace Mart.Customer.Application.Cashback.Queries.GetStoreWalletConfigurations;

public sealed class GetStoreWalletConfigurationsQueryHandler
    : IRequestHandler<GetStoreWalletConfigurationsQuery, StoreWalletConfigurationsPageDto>
{
    private readonly ICashbackConfigurationRepository _repository;

    public GetStoreWalletConfigurationsQueryHandler(ICashbackConfigurationRepository repository)
    {
        _repository = repository;
    }

    public async Task<StoreWalletConfigurationsPageDto> Handle(
        GetStoreWalletConfigurationsQuery request,
        CancellationToken cancellationToken)
    {
        var (items, totalRecords) = await _repository.GetStoreWalletConfigurationsPagedAsync(
            WalletTypeCodes.MartWallet,
            DateTime.UtcNow,
            request.PageNumber,
            request.PageSize,
            cancellationToken);

        return new StoreWalletConfigurationsPageDto(
            request.PageNumber,
            request.PageSize,
            totalRecords,
            (long)request.PageNumber * request.PageSize < totalRecords,
            items);
    }
}
