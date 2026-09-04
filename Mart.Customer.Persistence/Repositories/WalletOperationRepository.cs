using Mart.Customer.Application.Abstractions.Data;
using Mart.Customer.Application.Wallets.Engine;
using Mart.Customer.Domain.Wallets;
using Microsoft.EntityFrameworkCore;

namespace Mart.Customer.Persistence.Repositories;

internal sealed class WalletOperationRepository(ApplicationDbContext db) : IWalletOperationRepository
{
    public Task<WalletOperation?> FindAsync(string kind, string key, CancellationToken cancellationToken) =>
        db.WalletOperations.AsNoTracking().SingleOrDefaultAsync(
            x => x.OperationKind == kind && x.BusinessKey == key, cancellationToken);

    public Task AddAsync(WalletOperation operation, CancellationToken cancellationToken) =>
        db.WalletOperations.AddAsync(operation, cancellationToken).AsTask();

    public Task AddComponentAsync(WalletOperationComponent component, CancellationToken cancellationToken) =>
        db.WalletOperationComponents.AddAsync(component, cancellationToken).AsTask();

    public async Task<WalletEngineResult> ReadResultAsync(long id, bool replay, CancellationToken cancellationToken)
    {
        var operation = await db.WalletOperations.AsNoTracking()
            .Where(x => x.WalletOperationId == id)
            .Select(x => new { x.WalletOperationId, x.OperationNumber, x.OperationKind, x.Outcome, x.EffectiveAt })
            .SingleAsync(cancellationToken);
        var allocations = await db.WalletOperationComponents.AsNoTracking()
            .Where(x => x.WalletOperationId == id)
            .OrderBy(x => x.WalletOperationComponentId)
            .Select(x => new WalletPostedAllocation(x.CustomerWalletId, x.WalletTypeId, x.StoreId,
                x.ComponentCode, x.AllocationKey, x.Transaction.Amount, x.WalletTransactionId,
                x.Transaction.TransactionNumber, x.Transaction.BalanceBefore, x.Transaction.BalanceAfter, x.ExpiryDate))
            .ToListAsync(cancellationToken);
        return new WalletEngineResult(operation.WalletOperationId, operation.OperationNumber,
            operation.OperationKind, operation.Outcome!, replay, operation.EffectiveAt, allocations);
    }
}
