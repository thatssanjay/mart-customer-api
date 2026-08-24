using Mart.Customer.Application.Wallets.Dtos;
using Mart.Customer.Domain.Wallets;

namespace Mart.Customer.Application.Abstractions.Data;

public interface IWalletBalanceBucketRepository
{
    Task AddAsync(
        WalletBalanceBucket bucket,
        CancellationToken cancellationToken = default);

    Task<WalletExpirySummaryDto?> GetExpirySummaryAsync(
        long customerId,
        int walletTypeId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WalletBalanceBucketDto>> GetByCustomerWalletIdAsync(
        long customerWalletId,
        bool availableOnly,
        bool includeSourceTransaction,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WalletBalanceBucket>> GetEligibleForRedemptionAsync(
        long customerWalletId,
        DateTime asOfUtc,
        CancellationToken cancellationToken = default);
}
