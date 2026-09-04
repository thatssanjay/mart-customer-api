using System.Text.Json;
using Mart.Customer.Domain.Common;

namespace Mart.Customer.Domain.Wallets;

public sealed class WalletOperationComponent
{
    private WalletOperationComponent() { }

    public long WalletOperationComponentId { get; private set; }
    public long WalletOperationId { get; private set; }
    public long CustomerWalletId { get; private set; }
    public int WalletTypeId { get; private set; }
    public long? StoreId { get; private set; }
    public string ComponentCode { get; private set; } = string.Empty;
    public string AllocationKey { get; private set; } = string.Empty;
    public long WalletTransactionId { get; private set; }
    public WalletTransaction Transaction { get; private set; } = null!;
    public DateTime? ExpiryDate { get; private set; }
    public string CalculationSnapshotJson { get; private set; } = string.Empty;

    public static WalletOperationComponent Create(WalletOperation operation, CustomerWallet wallet,
        WalletTransaction transaction, string componentCode, string allocationKey,
        DateTime? expiryDate, WalletCalculationSnapshot snapshot)
    {
        if (operation.Status != WalletOperationStatuses.Processing ||
            operation.WalletOperationId <= 0 || wallet.CustomerId != operation.CustomerId ||
            transaction.CustomerWalletId != wallet.CustomerWalletId || transaction.TransactionType != "CREDIT")
            throw new DomainException("Invalid wallet operation component association.");
        if (snapshot.RoundedAmount.HasValue && snapshot.RoundedAmount != transaction.Amount)
            throw new DomainException("Snapshot amount must equal the posted amount.");
        var json = JsonSerializer.Serialize(snapshot with { RoundedAmount = transaction.Amount });
        if (json.Length > 16000)
            throw new DomainException("Wallet calculation snapshot is too large.");
        return new WalletOperationComponent
        {
            WalletOperationId = operation.WalletOperationId, CustomerWalletId = wallet.CustomerWalletId,
            WalletTypeId = wallet.WalletTypeId, StoreId = wallet.StoreId,
            ComponentCode = componentCode, AllocationKey = allocationKey, Transaction = transaction,
            ExpiryDate = expiryDate, CalculationSnapshotJson = json
        };
    }
}
