using System.Globalization;
using Mart.Customer.Domain.Common;

namespace Mart.Customer.Domain.Wallets;

public sealed class WalletOperation
{
    private WalletOperation() { }

    public long WalletOperationId { get; private set; }
    public string OperationNumber { get; private set; } = string.Empty;
    public string OperationKind { get; private set; } = string.Empty;
    public string BusinessKey { get; private set; } = string.Empty;
    public long CustomerId { get; private set; }
    public long? StoreId { get; private set; }
    public long? CustomerOrderId { get; private set; }
    public long? CustomerOrderPaymentId { get; private set; }
    public DateTime EffectiveAt { get; private set; }
    public string Status { get; private set; } = WalletOperationStatuses.Processing;
    public string? Outcome { get; private set; }
    public string CalculationVersion { get; private set; } = string.Empty;
    public DateTime CreatedOn { get; private set; }
    public string CreatedBy { get; private set; } = string.Empty;
    public DateTime? CompletedOn { get; private set; }

    public static string SaleBusinessKey(long orderId) =>
        $"{WalletOperationKinds.SaleReward}:{orderId.ToString(CultureInfo.InvariantCulture)}";

    public static WalletOperation CreateSaleReward(string number, string businessKey,
        long customerId, long? storeId, long orderId, long? paymentId,
        DateTime effectiveAt, string calculationVersion, string createdBy, DateTime createdOn)
    {
        if (customerId <= 0 || orderId <= 0 || storeId <= 0 || paymentId <= 0)
            throw new DomainException("Wallet operation identifiers must be positive.");
        if (businessKey != SaleBusinessKey(orderId))
            throw new DomainException("The sale reward business key must match its order.");
        if (effectiveAt.Kind != DateTimeKind.Utc || effectiveAt == default)
            throw new DomainException("Wallet effective time must be UTC.");
        if (string.IsNullOrWhiteSpace(number) || number.Length > 50 ||
            string.IsNullOrWhiteSpace(createdBy) || createdBy.Length > 100 ||
            string.IsNullOrWhiteSpace(calculationVersion) || calculationVersion.Length > 100)
            throw new DomainException("Valid operation number, calculation version and audit user are required.");

        return new WalletOperation
        {
            OperationNumber = number, OperationKind = WalletOperationKinds.SaleReward,
            BusinessKey = businessKey, CustomerId = customerId, StoreId = storeId,
            CustomerOrderId = orderId, CustomerOrderPaymentId = paymentId,
            EffectiveAt = effectiveAt, CalculationVersion = calculationVersion,
            CreatedBy = createdBy, CreatedOn = createdOn
        };
    }

    public void Complete(bool hasCredits, DateTime completedOn)
    {
        if (Status != WalletOperationStatuses.Processing)
            throw new DomainException("A completed wallet operation cannot be changed.");
        Status = WalletOperationStatuses.Completed;
        Outcome = hasCredits ? WalletOperationOutcomes.Credited : WalletOperationOutcomes.NoReward;
        CompletedOn = completedOn;
    }
}
