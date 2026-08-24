using Mart.Customer.Application.Abstractions.Data;
using Mart.Customer.Application.Wallets.Dtos;
using Mart.Customer.Domain.Wallets;
using Microsoft.EntityFrameworkCore;

namespace Mart.Customer.Persistence.Repositories;

internal sealed class WalletBalanceBucketRepository : IWalletBalanceBucketRepository
{
    private readonly ApplicationDbContext _dbContext;

    public WalletBalanceBucketRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task AddAsync(
        WalletBalanceBucket bucket,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.WalletBalanceBuckets.AddAsync(bucket, cancellationToken).AsTask();
    }

    public Task<WalletExpirySummaryDto?> GetExpirySummaryAsync(
        long customerId,
        int walletTypeId,
        CancellationToken cancellationToken = default)
    {
        return (
            from wallet in _dbContext.CustomerWallets.AsNoTracking()
            join walletType in _dbContext.WalletTypes.AsNoTracking()
                on wallet.WalletTypeId equals walletType.Id
            join bucket in _dbContext.WalletBalanceBuckets.AsNoTracking()
                    .Where(bucket => bucket.AvailableAmount > 0 && bucket.ExpiryDate != null)
                on wallet.CustomerWalletId equals bucket.CustomerWalletId into expiringBuckets
            where wallet.CustomerId == customerId &&
                  wallet.WalletTypeId == walletTypeId &&
                  wallet.IsActive &&
                  walletType.IsActive
            select new WalletExpirySummaryDto(
                wallet.CustomerWalletId,
                wallet.CustomerId,
                wallet.WalletTypeId,
                expiringBuckets.Sum(bucket => bucket.AvailableAmount),
                expiringBuckets.Min(bucket => bucket.ExpiryDate),
                expiringBuckets.Count()))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<WalletBalanceBucketDto>> GetByCustomerWalletIdAsync(
        long customerWalletId,
        bool availableOnly,
        bool includeSourceTransaction,
        CancellationToken cancellationToken = default)
    {
        IQueryable<WalletBalanceBucket> bucketQuery = _dbContext.WalletBalanceBuckets
            .AsNoTracking()
            .Where(bucket => bucket.CustomerWalletId == customerWalletId);

        if (availableOnly)
        {
            bucketQuery = bucketQuery.Where(bucket => bucket.AvailableAmount > 0);
        }

        if (!includeSourceTransaction)
        {
            return await bucketQuery
                .OrderBy(bucket => bucket.ExpiryDate == null)
                .ThenBy(bucket => bucket.ExpiryDate)
                .ThenBy(bucket => bucket.WalletBalanceBucketId)
                .Select(bucket => new WalletBalanceBucketDto(
                    bucket.WalletBalanceBucketId,
                    bucket.CustomerWalletId,
                    bucket.SourceTransactionId,
                    bucket.OriginalAmount,
                    bucket.AvailableAmount,
                    bucket.ExpiryDate,
                    bucket.CreatedOn,
                    null))
                .ToListAsync(cancellationToken);
        }

        var query =
            from bucket in bucketQuery
            join transaction in _dbContext.WalletTransactions.AsNoTracking()
                on bucket.SourceTransactionId equals transaction.WalletTransactionId into sourceTransactions
            from transaction in sourceTransactions.DefaultIfEmpty()
            orderby bucket.ExpiryDate == null, bucket.ExpiryDate, bucket.WalletBalanceBucketId
            select new WalletBalanceBucketDto(
                bucket.WalletBalanceBucketId,
                bucket.CustomerWalletId,
                bucket.SourceTransactionId,
                bucket.OriginalAmount,
                bucket.AvailableAmount,
                bucket.ExpiryDate,
                bucket.CreatedOn,
                transaction == null
                    ? null
                    : new WalletTransactionDto(
                        transaction.WalletTransactionId,
                        transaction.TransactionNumber,
                        transaction.TransactionType,
                        transaction.Amount,
                        transaction.BalanceBefore,
                        transaction.BalanceAfter,
                        transaction.ReferenceType,
                        transaction.ReferenceId,
                        transaction.Remarks,
                        transaction.TransactionDate));

        return await query.ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<WalletBalanceBucket>> GetEligibleForRedemptionAsync(
        long customerWalletId,
        DateTime asOfUtc,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.WalletBalanceBuckets
            .Where(bucket =>
                bucket.CustomerWalletId == customerWalletId &&
                bucket.AvailableAmount > 0 &&
                (!bucket.ExpiryDate.HasValue || bucket.ExpiryDate.Value > asOfUtc))
            .OrderBy(bucket => bucket.ExpiryDate == null)
            .ThenBy(bucket => bucket.ExpiryDate)
            .ThenBy(bucket => bucket.WalletBalanceBucketId)
            .ToListAsync(cancellationToken);
    }
}
