using Mart.Customer.Application.Cashback.Dtos;

namespace Mart.Customer.Application.Abstractions.Data;

public interface ICashbackConfigurationRepository
{
    Task<IReadOnlyList<CashbackSettingDto>> GetActiveAsync(
        DateTime currentDate,
        CancellationToken cancellationToken = default);
}
