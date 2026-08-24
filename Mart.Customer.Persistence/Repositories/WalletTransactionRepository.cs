using Mart.Customer.Application.Abstractions.Data;
using Mart.Customer.Application.Wallets.Dtos;
using Mart.Customer.Domain.Wallets;
using Microsoft.EntityFrameworkCore;

namespace Mart.Customer.Persistence.Repositories;

internal sealed class WalletTransactionRepository : IWalletTransactionRepository
{
    private readonly ApplicationDbContext _dbContext;

    public WalletTransactionRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task AddAsync(
        WalletTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.WalletTransactions.AddAsync(transaction, cancellationToken).AsTask();
    }

    public Task<CreditWalletResultDto?> GetCreditByReferenceAsync(
        long customerWalletId,
        string referenceType,
        long referenceId,
        CancellationToken cancellationToken = default)
    {
        return (
            from transaction in _dbContext.WalletTransactions.AsNoTracking()
            join wallet in _dbContext.CustomerWallets.AsNoTracking()
                on transaction.CustomerWalletId equals wallet.CustomerWalletId
            where transaction.CustomerWalletId == customerWalletId &&
                  transaction.TransactionType == "CREDIT" &&
                  transaction.ReferenceType == referenceType &&
                  transaction.ReferenceId == referenceId
            orderby transaction.WalletTransactionId
            select new CreditWalletResultDto(
                transaction.WalletTransactionId,
                transaction.TransactionNumber,
                transaction.CustomerWalletId,
                wallet.CustomerId,
                wallet.WalletTypeId,
                transaction.Amount,
                transaction.BalanceBefore,
                transaction.BalanceAfter,
                wallet.TotalCredit,
                transaction.ReferenceType,
                transaction.ReferenceId,
                _dbContext.WalletBalanceBuckets
                    .AsNoTracking()
                    .Where(bucket => bucket.SourceTransactionId == transaction.WalletTransactionId)
                    .Select(bucket => bucket.ExpiryDate)
                    .FirstOrDefault(),
                transaction.TransactionDate))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<RedeemWalletResultDto?> GetRedemptionByReferenceAsync(
        long customerWalletId,
        string referenceType,
        long referenceId,
        CancellationToken cancellationToken = default)
    {
        return (
            from transaction in _dbContext.WalletTransactions.AsNoTracking()
            join wallet in _dbContext.CustomerWallets.AsNoTracking()
                on transaction.CustomerWalletId equals wallet.CustomerWalletId
            where transaction.CustomerWalletId == customerWalletId &&
                  transaction.TransactionType == "REDEMPTION" &&
                  transaction.ReferenceType == referenceType &&
                  transaction.ReferenceId == referenceId
            orderby transaction.WalletTransactionId
            select new RedeemWalletResultDto(
                transaction.WalletTransactionId,
                transaction.TransactionNumber,
                transaction.CustomerWalletId,
                wallet.CustomerId,
                wallet.WalletTypeId,
                transaction.Amount,
                transaction.BalanceBefore,
                transaction.BalanceAfter,
                wallet.TotalDebit,
                transaction.ReferenceType,
                transaction.ReferenceId,
                transaction.TransactionDate))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<WalletTransaction?> GetRefundableByTransactionNumberAsync(
        string transactionNumber,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.WalletTransactions
            .AsNoTracking()
            .SingleOrDefaultAsync(
                transaction => transaction.TransactionNumber == transactionNumber,
                cancellationToken);
    }

    public Task<decimal> GetRefundedAmountAsync(
        long originalTransactionId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.WalletTransactions
            .AsNoTracking()
            .Where(transaction =>
                transaction.TransactionType == "REFUND" &&
                transaction.ReferenceType == WalletTransaction.RefundReferenceType &&
                transaction.ReferenceId == originalTransactionId)
            .SumAsync(transaction => transaction.Amount, cancellationToken);
    }

    public Task<WalletTransactionDetailDto?> GetByTransactionNumberAsync(
        string transactionNumber,
        CancellationToken cancellationToken = default)
    {
        var query =
            from transaction in _dbContext.WalletTransactions.AsNoTracking()
            join wallet in _dbContext.CustomerWallets.AsNoTracking()
                on transaction.CustomerWalletId equals wallet.CustomerWalletId
            join walletType in _dbContext.WalletTypes.AsNoTracking()
                on wallet.WalletTypeId equals walletType.Id
            where transaction.TransactionNumber == transactionNumber
            select new WalletTransactionDetailDto(
                transaction.WalletTransactionId,
                transaction.TransactionNumber,
                transaction.CustomerWalletId,
                wallet.CustomerId,
                wallet.WalletTypeId,
                walletType.Name,
                walletType.Code,
                wallet.IsActive,
                walletType.IsActive,
                transaction.TransactionType,
                transaction.Amount,
                transaction.BalanceBefore,
                transaction.BalanceAfter,
                transaction.ReferenceType,
                transaction.ReferenceId,
                transaction.Remarks,
                transaction.TransactionDate,
                transaction.CreatedBy);

        return query.SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<WalletTransactionDto> Transactions, int TotalCount)> GetPagedAsync(
        long customerWalletId,
        int pageNumber,
        int pageSize,
        string? transactionType,
        DateTime? fromDate,
        DateTime? toDate,
        string? referenceType,
        long? referenceId,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.WalletTransactions
            .AsNoTracking()
            .Where(transaction => transaction.CustomerWalletId == customerWalletId);

        if (!string.IsNullOrWhiteSpace(transactionType))
        {
            var normalizedTransactionType = transactionType.Trim();
            query = query.Where(transaction => transaction.TransactionType == normalizedTransactionType);
        }

        if (fromDate.HasValue)
        {
            query = query.Where(transaction => transaction.TransactionDate >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(transaction => transaction.TransactionDate <= toDate.Value);
        }

        if (!string.IsNullOrWhiteSpace(referenceType))
        {
            var normalizedReferenceType = referenceType.Trim();
            query = query.Where(transaction => transaction.ReferenceType == normalizedReferenceType);
        }

        if (referenceId.HasValue)
        {
            query = query.Where(transaction => transaction.ReferenceId == referenceId.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var transactions = await query
            .OrderByDescending(transaction => transaction.TransactionDate)
            .ThenByDescending(transaction => transaction.WalletTransactionId)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(transaction => new WalletTransactionDto(
                transaction.WalletTransactionId,
                transaction.TransactionNumber,
                transaction.TransactionType,
                transaction.Amount,
                transaction.BalanceBefore,
                transaction.BalanceAfter,
                transaction.ReferenceType,
                transaction.ReferenceId,
                transaction.Remarks,
                transaction.TransactionDate))
            .ToListAsync(cancellationToken);

        return (transactions, totalCount);
    }
}
