using Mart.Customer.Application.Cashback.Dtos;

namespace Mart.Customer.Application.Abstractions.Data;

public interface ICashbackConfigurationRepository
{
    Task<IReadOnlyList<CashbackSettingDto>> GetActiveAsync(
        DateTime currentDate,
        CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<StoreWalletConfigurationDto> Items, int TotalRecords)>
        GetStoreWalletConfigurationsPagedAsync(
            string walletCode,
            DateTime currentDate,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken = default);

    Task<ActiveStoreWalletConversionDto?> GetActiveStoreWalletConversionAsync(
        long storeId,
        string walletCode,
        DateTime currentDate,
        CancellationToken cancellationToken = default);
}
