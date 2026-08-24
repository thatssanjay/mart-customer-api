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

    public Task<long?> GetActiveIdAsync(
        long customerId,
        int walletTypeId,
        CancellationToken cancellationToken = default)
    {
        return (
            from wallet in _dbContext.CustomerWallets.AsNoTracking()
            join walletType in _dbContext.WalletTypes.AsNoTracking()
                on wallet.WalletTypeId equals walletType.Id
            where wallet.CustomerId == customerId &&
                  wallet.WalletTypeId == walletTypeId &&
                  wallet.IsActive &&
                  walletType.IsActive
            select (long?)wallet.CustomerWalletId)
            .SingleOrDefaultAsync(cancellationToken);
    }

    public Task<CustomerWalletDto?> GetDetailAsync(
        long customerId,
        int walletTypeId,
        CancellationToken cancellationToken = default)
    {
        return (
            from wallet in _dbContext.CustomerWallets.AsNoTracking()
            join walletType in _dbContext.WalletTypes.AsNoTracking()
                on wallet.WalletTypeId equals walletType.Id
            where wallet.CustomerId == customerId &&
                  wallet.WalletTypeId == walletTypeId &&
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
                wallet.ModifiedOn))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public Task<CustomerWalletDto?> GetDetailIncludingInactiveAsync(
        long customerId,
        int walletTypeId,
        CancellationToken cancellationToken = default)
    {
        return (
            from wallet in _dbContext.CustomerWallets.AsNoTracking()
            join walletType in _dbContext.WalletTypes.AsNoTracking()
                on wallet.WalletTypeId equals walletType.Id
            where wallet.CustomerId == customerId &&
                  wallet.WalletTypeId == walletTypeId
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
                wallet.ModifiedOn))
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
                item.Wallet.ModifiedOn))
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
        CancellationToken cancellationToken = default)
    {
        return _dbContext.CustomerWallets.SingleOrDefaultAsync(
            wallet => wallet.CustomerId == customerId && wallet.WalletTypeId == walletTypeId,
            cancellationToken);
    }

    public Task AddRangeAsync(
        IEnumerable<CustomerWallet> wallets,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.CustomerWallets.AddRangeAsync(wallets, cancellationToken);
    }
}
