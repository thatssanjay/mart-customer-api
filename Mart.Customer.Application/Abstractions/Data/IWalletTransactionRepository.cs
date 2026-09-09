using Mart.Customer.Application.Wallets.Dtos;
using Mart.Customer.Domain.Wallets;

namespace Mart.Customer.Application.Abstractions.Data;

public interface IWalletTransactionRepository
{
    Task AddAsync(
        WalletTransaction transaction,
        CancellationToken cancellationToken = default);

    Task<CreditWalletResultDto?> GetCreditByReferenceAsync(
        long customerWalletId,
        string referenceType,
        long referenceId,
        CancellationToken cancellationToken = default);

    Task<RedeemWalletResultDto?> GetRedemptionByReferenceAsync(
        long customerWalletId,
        string referenceType,
        long referenceId,
        CancellationToken cancellationToken = default);

    Task<WalletTransaction?> GetRefundableByTransactionNumberAsync(
        string transactionNumber,
        CancellationToken cancellationToken = default);

    Task<decimal> GetRefundedAmountAsync(
        long originalTransactionId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WalletTransaction>> GetByReferenceAsync(
        long customerId,
        string referenceType,
        long referenceId,
        CancellationToken cancellationToken = default);

    Task<WalletTransactionDetailDto?> GetByTransactionNumberAsync(
        string transactionNumber,
        CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<WalletTransactionDto> Transactions, int TotalCount)> GetPagedAsync(
        long customerWalletId,
        int pageNumber,
        int pageSize,
        string? transactionType,
        DateTime? fromDate,
        DateTime? toDate,
        string? referenceType,
        long? referenceId,
        CancellationToken cancellationToken = default);
}
