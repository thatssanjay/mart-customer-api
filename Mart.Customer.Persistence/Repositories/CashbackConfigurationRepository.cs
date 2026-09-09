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
            .Where(setting => setting.IsActive == true &&
                              setting.StartDate <= currentDate &&
                              setting.EndDate >= currentDate)
            .OrderBy(setting => setting.StoreId)
            .Select(setting => new CashbackSettingDto(
                setting.StoreId ?? 0,
                setting.CashbackPercentage ?? 0,
                setting.CashbackValidityDays ?? 0,
                setting.MinimumPurchaseAmount ?? 0,
                setting.MaximumCashbackPerOrder ?? 0,
                setting.IsActive ?? false))
            .ToListAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<StoreWalletConfigurationDto> Items, int TotalRecords)>
        GetStoreWalletConfigurationsPagedAsync(
            string walletCode,
            DateTime currentDate,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken = default)
    {
        var eligibleConfigurations =
            from setting in _dbContext.CashbackConfigurations.AsNoTracking()
            join walletSetting in _dbContext.CashbackSettingWallets.AsNoTracking()
                on setting.CashbackSettingId equals walletSetting.CashbackSettingId
            join walletType in _dbContext.WalletTypes.AsNoTracking()
                on walletSetting.WalletTypeId equals walletType.Id
            join store in _dbContext.MartStores.AsNoTracking()
                on setting.StoreId equals store.StoreId
            where setting.StoreId.HasValue &&
                  setting.IsActive == true &&
                  (setting.StartDate == null || setting.StartDate <= currentDate) &&
                  (setting.EndDate == null || setting.EndDate >= currentDate) &&
                  walletSetting.IsActive &&
                  walletSetting.ConversionRate.HasValue &&
                  walletSetting.ConversionRate > 0 &&
                  (walletSetting.IsNoExpiry ||
                   ((walletSetting.StartDate == null || walletSetting.StartDate <= currentDate) &&
                    (walletSetting.EndDate == null || walletSetting.EndDate > currentDate))) &&
                  walletType.IsActive &&
                  walletType.Code.ToUpper() == walletCode &&
                  store.IsActive
            select new
            {
                StoreId = setting.StoreId.GetValueOrDefault(),
                setting.CashbackSettingId,
                store.StoreName,
                store.City,
                ConversionRate = walletSetting.ConversionRate.GetValueOrDefault()
            };

        var newestSettingPerStore = eligibleConfigurations
            .GroupBy(configuration => configuration.StoreId)
            .Select(group => new
            {
                StoreId = group.Key,
                CashbackSettingId = group.Max(configuration => configuration.CashbackSettingId)
            });

        var distinctConfigurations =
            from configuration in eligibleConfigurations
            join newest in newestSettingPerStore
                on new { configuration.StoreId, configuration.CashbackSettingId }
                equals new { newest.StoreId, newest.CashbackSettingId }
            select configuration;

        var totalRecords = await distinctConfigurations.CountAsync(cancellationToken);
        var rows = await distinctConfigurations
            .OrderByDescending(configuration => configuration.ConversionRate)
            .ThenBy(configuration => configuration.StoreId)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = rows
            .Select(configuration => new StoreWalletConfigurationDto(
                configuration.StoreId,
                configuration.StoreName,
                configuration.City,
                configuration.ConversionRate))
            .ToList();

        return (items, totalRecords);
    }

    public Task<ActiveStoreWalletConversionDto?> GetActiveStoreWalletConversionAsync(
        long storeId,
        string walletCode,
        DateTime currentDate,
        CancellationToken cancellationToken = default)
    {
        return (
            from setting in _dbContext.CashbackConfigurations.AsNoTracking()
            join walletSetting in _dbContext.CashbackSettingWallets.AsNoTracking()
                on setting.CashbackSettingId equals walletSetting.CashbackSettingId
            join walletType in _dbContext.WalletTypes.AsNoTracking()
                on walletSetting.WalletTypeId equals walletType.Id
            join store in _dbContext.MartStores.AsNoTracking()
                on setting.StoreId equals store.StoreId
            where setting.StoreId == storeId &&
                  setting.IsActive == true &&
                  (setting.StartDate == null || setting.StartDate <= currentDate) &&
                  (setting.EndDate == null || setting.EndDate >= currentDate) &&
                  walletSetting.IsActive &&
                  walletSetting.ConversionRate.HasValue &&
                  walletSetting.ConversionRate > 0 &&
                  (walletSetting.IsNoExpiry ||
                   ((walletSetting.StartDate == null || walletSetting.StartDate <= currentDate) &&
                    (walletSetting.EndDate == null || walletSetting.EndDate > currentDate))) &&
                  walletType.IsActive &&
                  walletType.Code.ToUpper() == walletCode &&
                  store.IsActive
            orderby setting.CashbackSettingId descending, walletSetting.Id descending
            select new ActiveStoreWalletConversionDto(
                store.StoreId,
                store.StoreName,
                walletType.Id,
                setting.CashbackSettingId,
                walletSetting.Id,
                walletSetting.ConversionRate.GetValueOrDefault(),
                walletSetting.IsNoExpiry ? null : walletSetting.EndDate))
            .FirstOrDefaultAsync(cancellationToken);
    }
}
