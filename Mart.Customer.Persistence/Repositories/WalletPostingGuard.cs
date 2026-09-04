using System.Data;
using Mart.Customer.Application.Abstractions.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Mart.Customer.Persistence.Repositories;

internal sealed class WalletPostingGuard(ApplicationDbContext db) : IWalletPostingGuard
{
    public void EnsureCleanEntry()
    {
        if (!db.Database.IsRelational() || db.Database.CurrentTransaction is not null ||
            System.Transactions.Transaction.Current is not null || db.ChangeTracker.HasChanges())
            throw new InvalidOperationException("Wallet engine requires a clean relational context and owns its transaction.");
        if (db.Database.CreateExecutionStrategy().RetriesOnFailure)
            throw new InvalidOperationException("Wallet engine retries must own the complete operation with clean tracked state.");
        // Unmodified tracked entities may still contain stale wallet balances from an earlier operation.
        db.ChangeTracker.Clear();
    }

    public void EnsureTransaction()
    {
        if (!db.Database.IsRelational() || db.Database.CurrentTransaction is null ||
            db.Database.CurrentTransaction.GetDbTransaction().IsolationLevel != IsolationLevel.Serializable)
            throw new InvalidOperationException("Wallet posting requires the engine's Serializable transaction.");
    }

    public void EnsureTracked(object entity)
    {
        if (db.Entry(entity).State == EntityState.Detached)
            throw new InvalidOperationException("Wallet posting entities must be tracked by the posting context.");
    }

    public bool IsRetryableConflict(Exception exception)
    {
        // The non-retrying SQL Server execution strategy wraps transient DbUpdateException
        // failures in InvalidOperationException. Inspect the cause, not only the top-level type.
        SqlException? sql = null;
        for (Exception? cause = exception; cause is not null; cause = cause.InnerException)
        {
            if (cause is SqlException sqlException)
            {
                sql = sqlException;
                break;
            }
        }
        if (sql is null) return false;
        if (sql.Number == 1205) return true;
        if (sql.Number is not (2601 or 2627)) return false;
        // Component/bucket constraint failures indicate a programming/data error, not a retryable race.
        return new[] { "UQ_CustomerWallet", "UQ_WalletOperation_Identity", "UQ_WalletOperation_Number",
                "UQ_WalletTransaction_TransactionNumber", "UQ_WalletOperation_Order" }
            .Any(name => sql.Message.Contains($"'{name}'", StringComparison.Ordinal));
    }
}
