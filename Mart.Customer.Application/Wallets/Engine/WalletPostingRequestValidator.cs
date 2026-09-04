using FluentValidation;
using Mart.Customer.Domain.Common;
using Mart.Customer.Domain.Wallets;

namespace Mart.Customer.Application.Wallets.Engine;

public sealed class WalletPostingRequestValidator : AbstractValidator<WalletPostingRequest>
{
    public WalletPostingRequestValidator()
    {
        RuleFor(x => x.OperationKind).Equal(WalletOperationKinds.SaleReward);
        RuleFor(x => x.CustomerId).GreaterThan(0);
        RuleFor(x => x.CustomerOrderId).GreaterThan(0);
        RuleFor(x => x.StoreId).Must(x => x is null or > 0);
        RuleFor(x => x.CustomerOrderPaymentId).Must(x => x is null or > 0);
        RuleFor(x => x.BusinessKey).Must((request, key) => key == WalletOperation.SaleBusinessKey(request.CustomerOrderId));
        RuleFor(x => x.EffectiveAt).Must(x => x != default && x.Kind == DateTimeKind.Utc);
        RuleFor(x => x.CreatedBy).NotEmpty().MaximumLength(100);
        RuleFor(x => x.CalculationVersion).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Allocations).NotNull().Must(x => x is null || x.Count <= 1000);
    }

    internal static void ValidateComponent(WalletAllocationResult allocation)
    {
        if (allocation.WalletTypeId <= 0 || allocation.StoreId <= 0 || allocation.Snapshot is null ||
            allocation.Snapshot.SchemaVersion != 1)
            throw new DomainException("Invalid wallet allocation identifiers or snapshot.");
        if (allocation.ComponentCode is not (WalletComponentCodes.BaseReward or WalletComponentCodes.SubscriptionBonus))
            throw new DomainException("This credit component is not supported in Phase 2.");
        if (string.IsNullOrWhiteSpace(allocation.AllocationKey) || allocation.AllocationKey.Length > 100 ||
            allocation.AllocationKey.Any(c => !(c is >= 'A' and <= 'Z' or >= '0' and <= '9' or '_' or '-' or ':' or '.')))
            throw new DomainException("Allocation key must be a stable uppercase ASCII identifier.");
        if (allocation.Amount < 0 || allocation.Amount > 9999999999999999.99m ||
            allocation.Amount != WalletRoundingPolicy.RoundAmount(allocation.Amount))
            throw new DomainException("Allocation amount must be non-negative and fit decimal(18,2).");
        if (allocation.Snapshot.RoundedAmount.HasValue && allocation.Snapshot.RoundedAmount != allocation.Amount)
            throw new DomainException("Snapshot amount must equal the allocation amount.");
    }
}
