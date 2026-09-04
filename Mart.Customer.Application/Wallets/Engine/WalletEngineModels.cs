using Mart.Customer.Domain.Wallets;

namespace Mart.Customer.Application.Wallets.Engine;

/// <summary>Trusted internal contract; never bind directly to an HTTP request.</summary>
public sealed record WalletPostingRequest(
    string OperationKind, string BusinessKey, long CustomerId, long? StoreId,
    long CustomerOrderId, long? CustomerOrderPaymentId, DateTime EffectiveAt,
    string CalculationVersion, string CreatedBy, IReadOnlyList<WalletAllocationResult> Allocations);

public sealed record WalletAllocationResult(
    int WalletTypeId, long? StoreId, string ComponentCode, decimal Amount,
    DateTime? ExpiryDate, WalletCalculationSnapshot Snapshot,
    string AllocationKey = WalletComponentCodes.DefaultAllocation);

public sealed record WalletCalculationContext(
    long CustomerId, long StoreId, long CustomerOrderId, long CustomerOrderPaymentId,
    DateTime EffectiveAt, decimal EligibleAmount);

public sealed record WalletCalculationResult(IReadOnlyList<WalletAllocationResult> Allocations);

public interface IWalletCalculationService
{
    WalletCalculationResult Calculate(WalletCalculationContext context);
}

public sealed record WalletEngineResult(
    long WalletOperationId, string OperationNumber, string OperationKind, string Outcome,
    bool IsIdempotentReplay, DateTime EffectiveAt, IReadOnlyList<WalletPostedAllocation> Allocations);

public sealed record WalletPostedAllocation(
    long CustomerWalletId, int WalletTypeId, long? StoreId, string ComponentCode,
    string AllocationKey, decimal Amount, long WalletTransactionId, string TransactionNumber,
    decimal BalanceBefore, decimal BalanceAfter, DateTime? ExpiryDate);
