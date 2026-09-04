using Mart.Customer.Application.Wallets.Engine;
using Mart.Customer.Domain.Wallets;

namespace Mart.Customer.Application.Abstractions.Data;

public interface IWalletOperationRepository
{
    Task<WalletOperation?> FindAsync(string kind, string businessKey, CancellationToken cancellationToken);
    Task AddAsync(WalletOperation operation, CancellationToken cancellationToken);
    Task AddComponentAsync(WalletOperationComponent component, CancellationToken cancellationToken);
    Task<WalletEngineResult> ReadResultAsync(long operationId, bool replay, CancellationToken cancellationToken);
}

/// <summary>Protects the engine's ownership of a clean, relational posting transaction.</summary>
public interface IWalletPostingGuard
{
    void EnsureCleanEntry();
    void EnsureTransaction();
    void EnsureTracked(object entity);
    bool IsRetryableConflict(Exception exception);
}
