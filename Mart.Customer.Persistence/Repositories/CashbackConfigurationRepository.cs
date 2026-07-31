using Mart.Customer.Application.Abstractions.Data;
using Mart.Customer.Application.Cashback.Dtos;
using Microsoft.EntityFrameworkCore;

namespace Mart.Customer.Persistence.Repositories;

internal sealed class CashbackConfigurationRepository : ICashbackConfigurationRepository
{
    private readonly ApplicationDbContext _dbContext;

    public CashbackConfigurationRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<CashbackSettingDto>> GetActiveAsync(
        DateTime currentDate,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.CashbackConfigurations
            .AsNoTracking()
            .Where(setting => setting.IsActive &&
                              setting.StartDate <= currentDate &&
                              setting.EndDate >= currentDate)
            .OrderBy(setting => setting.StoreId)
            .Select(setting => new CashbackSettingDto(
                setting.StoreId,
                setting.CashbackPercentage,
                setting.CashbackValidityDays,
                setting.MinimumPurchaseAmount,
                setting.MaximumCashbackPerOrder,
                setting.IsActive))
            .ToListAsync(cancellationToken);
    }
}
