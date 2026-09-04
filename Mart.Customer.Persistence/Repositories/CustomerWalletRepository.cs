using Mart.Customer.Application.Abstractions.Data;
using Mart.Customer.Application.Wallets.Dtos;
using Mart.Customer.Domain.Wallets;
using Microsoft.EntityFrameworkCore;

namespace Mart.Customer.Persistence.Repositories;

internal sealed class CustomerWalletRepository : ICustomerWalletRepository
{
    private readonly ApplicationDbContext _dbContext;

    public CustomerWalletRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<CustomerWalletBalanceDto>> GetBalancesByCustomerIdAsync(
        long customerId,
        CancellationToken cancellationToken = default)
    {
        var activeWallets = _dbContext.CustomerWallets.AsNoTracking()
            .Where(wallet => wallet.CustomerId == customerId && wallet.IsActive);
        var activeTypes = _dbContext.WalletTypes.AsNoTracking()
            .Where(walletType => walletType.IsActive);

        // Global wallets use the existing unique customer/type/null-store scope.
        var globalBalances =
            from walletType in activeTypes
            where walletType.Code != WalletTypeCodes.MartWallet
            join wallet in activeWallets.Where(wallet => wallet.StoreId == null)
                on walletType.Id equals wallet.WalletTypeId into wallets
            from wallet in wallets.DefaultIfEmpty()
            select new
            {
                WalletTypeId = walletType.Id,
                WalletName = walletType.Name,
                WalletCode = walletType.Code,
                walletType.DisplayOrder,
                StoreId = (long?)null,
                StoreCode = (string?)null,
                StoreName = (string?)null,
                Balance = wallet == null ? 0m : wallet.CurrentBalance
            };

        var storeBalances =
            from walletType in activeTypes
            where walletType.Code == WalletTypeCodes.MartWallet
            join wallet in activeWallets on walletType.Id equals wallet.WalletTypeId
            join store in _dbContext.MartStores.AsNoTracking().Where(store => store.IsActive)
                on wallet.StoreId equals (long?)store.StoreId
            select new
            {
                WalletTypeId = walletType.Id,
                WalletName = walletType.Name,
                WalletCode = walletType.Code,
                walletType.DisplayOrder,
                StoreId = (long?)store.StoreId,
                store.StoreCode,
                StoreName = (string?)store.StoreName,
                Balance = wallet.CurrentBalance
            };

        return await globalBalances.Concat(storeBalances)
            .OrderBy(wallet => wallet.DisplayOrder)
            .ThenBy(wallet => wallet.WalletTypeId)
            .ThenBy(wallet => wallet.StoreId)
            .Select(wallet => new CustomerWalletBalanceDto(
                wallet.WalletTypeId, wallet.WalletName, wallet.WalletCode,
                wallet.DisplayOrder, wallet.StoreId, wallet.StoreCode,
                wallet.StoreName, wallet.Balance))
            .ToListAsync(cancellationToken);
    }

    public Task<long?> GetActiveIdAsync(
        long customerId,
        int walletTypeId,
        CancellationToken cancellationToken = default,
        long? storeId = null)
    {
        return (
            from wallet in _dbContext.CustomerWallets.AsNoTracking()
            join walletType in _dbContext.WalletTypes.AsNoTracking()
                on wallet.WalletTypeId equals walletType.Id
            where wallet.CustomerId == customerId &&
                  wallet.WalletTypeId == walletTypeId &&
                  (walletType.Code.ToUpper() == WalletTypeCodes.MartWallet ? storeId != null && wallet.StoreId == storeId : wallet.StoreId == null) &&
                  wallet.IsActive &&
                  walletType.IsActive
            select (long?)wallet.CustomerWalletId)
            .SingleOrDefaultAsync(cancellationToken);
    }

    public Task<CustomerWalletDto?> GetDetailAsync(
        long customerId,
        int walletTypeId,
        CancellationToken cancellationToken = default,
        long? storeId = null)
    {
        return (
            from wallet in _dbContext.CustomerWallets.AsNoTracking()
            join walletType in _dbContext.WalletTypes.AsNoTracking()
                on wallet.WalletTypeId equals walletType.Id
            where wallet.CustomerId == customerId &&
                  wallet.WalletTypeId == walletTypeId &&
                  (walletType.Code.ToUpper() == WalletTypeCodes.MartWallet ? storeId != null && wallet.StoreId == storeId : wallet.StoreId == null) &&
                  wallet.IsActive &&
                  walletType.IsActive
            select new CustomerWalletDto(
                wallet.CustomerWalletId,
                wallet.CustomerId,
                wallet.WalletTypeId,
                walletType.Name,
                walletType.Code,
                walletType.Description,
                wallet.CurrentBalance,
                wallet.TotalCredit,
                wallet.TotalDebit,
                wallet.TotalExpired,
                wallet.IsActive,
                walletType.IsActive,
                wallet.CreatedOn,
                wallet.ModifiedOn,
                wallet.StoreId))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public Task<CustomerWalletDto?> GetDetailIncludingInactiveAsync(
        long customerId,
        int walletTypeId,
        CancellationToken cancellationToken = default,
        long? storeId = null)
    {
        return (
            from wallet in _dbContext.CustomerWallets.AsNoTracking()
            join walletType in _dbContext.WalletTypes.AsNoTracking()
                on wallet.WalletTypeId equals walletType.Id
            where wallet.CustomerId == customerId &&
                  wallet.WalletTypeId == walletTypeId &&
                  (walletType.Code.ToUpper() == WalletTypeCodes.MartWallet ? storeId != null && wallet.StoreId == storeId : wallet.StoreId == null)
            select new CustomerWalletDto(
                wallet.CustomerWalletId,
                wallet.CustomerId,
                wallet.WalletTypeId,
                walletType.Name,
                walletType.Code,
                walletType.Description,
                wallet.CurrentBalance,
                wallet.TotalCredit,
                wallet.TotalDebit,
                wallet.TotalExpired,
                wallet.IsActive,
                walletType.IsActive,
                wallet.CreatedOn,
                wallet.ModifiedOn,
                wallet.StoreId))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CustomerWalletDto>> GetDetailsByCustomerIdAsync(
        long customerId,
        bool includeInactive,
        CancellationToken cancellationToken = default)
    {
        var query =
            from wallet in _dbContext.CustomerWallets.AsNoTracking()
            join walletType in _dbContext.WalletTypes.AsNoTracking()
                on wallet.WalletTypeId equals walletType.Id
            where wallet.CustomerId == customerId
            select new { Wallet = wallet, WalletType = walletType };

        if (!includeInactive)
        {
            query = query.Where(item => item.Wallet.IsActive && item.WalletType.IsActive);
        }

        return await query
            .OrderBy(item => item.WalletType.Name)
            .ThenBy(item => item.Wallet.CustomerWalletId)
            .Select(item => new CustomerWalletDto(
                item.Wallet.CustomerWalletId,
                item.Wallet.CustomerId,
                item.Wallet.WalletTypeId,
                item.WalletType.Name,
                item.WalletType.Code,
                item.WalletType.Description,
                item.Wallet.CurrentBalance,
                item.Wallet.TotalCredit,
                item.Wallet.TotalDebit,
                item.Wallet.TotalExpired,
                item.Wallet.IsActive,
                item.WalletType.IsActive,
                item.Wallet.CreatedOn,
                item.Wallet.ModifiedOn,
                item.Wallet.StoreId))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CustomerWallet>> GetByCustomerIdAsync(
        long customerId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.CustomerWallets
            .AsNoTracking()
            .Where(wallet => wallet.CustomerId == customerId)
            .ToListAsync(cancellationToken);
    }

    public Task<CustomerWallet?> GetByCustomerAndTypeAsync(
        long customerId,
        int walletTypeId,
        CancellationToken cancellationToken = default,
        long? storeId = null)
    {
        return (from wallet in _dbContext.CustomerWallets
                join walletType in _dbContext.WalletTypes on wallet.WalletTypeId equals walletType.Id
                where wallet.CustomerId == customerId && wallet.WalletTypeId == walletTypeId &&
                      (walletType.Code.ToUpper() == WalletTypeCodes.MartWallet ? storeId != null && wallet.StoreId == storeId : wallet.StoreId == null)
                select wallet).SingleOrDefaultAsync(cancellationToken);
    }

    public Task AddRangeAsync(
        IEnumerable<CustomerWallet> wallets,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.CustomerWallets.AddRangeAsync(wallets, cancellationToken);
    }
}
