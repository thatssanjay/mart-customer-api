using Mart.Customer.Application.Abstractions.Data;
using Mart.Customer.Application.Common.Utilities;
using Mart.Customer.Domain.Common;
using Mart.Customer.Domain.Wallets;

namespace Mart.Customer.Application.Wallets.Engine;

public interface IWalletLedgerService
{
    Task PostCreditAsync(WalletOperation operation, CustomerWallet wallet, WalletAllocationResult allocation,
        CancellationToken cancellationToken = default);
}

public sealed class WalletLedgerService(IWalletTransactionRepository transactions,
    IWalletBalanceBucketRepository buckets, IWalletOperationRepository operations,
    IWalletPostingGuard guard) : IWalletLedgerService
{
    public async Task PostCreditAsync(WalletOperation operation, CustomerWallet wallet,
        WalletAllocationResult allocation, CancellationToken cancellationToken = default)
    {
        guard.EnsureTransaction();
        guard.EnsureTracked(operation);
        guard.EnsureTracked(wallet);
        if (operation.Status != WalletOperationStatuses.Processing || wallet.CustomerId != operation.CustomerId ||
            wallet.WalletTypeId != allocation.WalletTypeId ||
            (wallet.StoreId.HasValue && wallet.StoreId != operation.StoreId))
            throw new DomainException("The posting does not belong to this operation and wallet.");
        if (allocation.Amount <= 0)
            throw new DomainException("Credit amount must be greater than zero.");
        if (allocation.ExpiryDate.HasValue && (allocation.ExpiryDate.Value.Kind != DateTimeKind.Utc ||
            allocation.ExpiryDate <= operation.EffectiveAt))
            throw new DomainException("Credit expiry must be UTC and later than the effective time.");
        WalletPostingRequestValidator.ValidateComponent(allocation);
        var before = wallet.CurrentBalance;
        wallet.Credit(allocation.Amount, DateTime.UtcNow);
        var transaction = WalletTransaction.CreateCredit(
            ReferenceCodeGenerator.GenerateWithPrefix("TX", 9), wallet.CustomerWalletId,
            allocation.Amount, before, wallet.CurrentBalance, "ORDER", operation.CustomerOrderId,
            allocation.ComponentCode, operation.EffectiveAt, operation.CreatedBy);
        var bucket = WalletBalanceBucket.Create(wallet.CustomerWalletId, transaction, allocation.Amount,
            allocation.ExpiryDate, operation.EffectiveAt);
        var component = WalletOperationComponent.Create(operation, wallet, transaction,
            allocation.ComponentCode, allocation.AllocationKey, allocation.ExpiryDate, allocation.Snapshot);
        await transactions.AddAsync(transaction, cancellationToken);
        await buckets.AddAsync(bucket, cancellationToken);
        await operations.AddComponentAsync(component, cancellationToken);
    }
}
