using Mart.Customer.Application.Abstractions.Data;
using Mart.Customer.Application.Wallets.Dtos;
using Microsoft.EntityFrameworkCore;

namespace Mart.Customer.Persistence.Repositories;

internal sealed class WalletTypeRepository : IWalletTypeRepository
{
    private readonly ApplicationDbContext _dbContext;

    public WalletTypeRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<WalletTypeDto?> GetByIdAsync(
        int walletTypeId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.WalletTypes
            .AsNoTracking()
            .Where(walletType => walletType.Id == walletTypeId)
            .Select(walletType => new WalletTypeDto(
                walletType.Id,
                walletType.Name,
                walletType.Code,
                walletType.Description,
                walletType.IsActive))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<WalletTypeDto>> GetAsync(
        bool activeOnly,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.WalletTypes.AsNoTracking();

        if (activeOnly)
        {
            query = query.Where(walletType => walletType.IsActive);
        }

        return await query
            .OrderBy(walletType => walletType.Name)
            .ThenBy(walletType => walletType.Id)
            .Select(walletType => new WalletTypeDto(
                walletType.Id,
                walletType.Name,
                walletType.Code,
                walletType.Description,
                walletType.IsActive))
            .ToListAsync(cancellationToken);
    }
}
