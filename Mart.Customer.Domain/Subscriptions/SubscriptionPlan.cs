using Mart.Customer.Domain.Common;

namespace Mart.Customer.Domain.Subscriptions;

public sealed class SubscriptionPlan
{
    private SubscriptionPlan()
    {
    }

    public int SubscriptionId { get; private set; }
    public string PlanName { get; private set; } = string.Empty;
    public decimal SubscriptionFee { get; private set; }
    public int DurationValue { get; private set; }
    public string DurationType { get; private set; } = string.Empty;
    public decimal ExtraPointPercentage { get; private set; }
    public decimal FeeToWalletPercentage { get; private set; }
    public int WalletTypeId { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime EffectiveFrom { get; private set; }
    public DateTime EffectiveTo { get; private set; }
    public DateTime CreatedOn { get; private set; }
    public int? CreatedBy { get; private set; }
    public DateTime? ModifiedOn { get; private set; }
    public int? ModifiedBy { get; private set; }

    public bool IsEffectiveOn(DateTime dateTime) =>
        IsActive && dateTime >= EffectiveFrom && dateTime <= EffectiveTo;

    public DateTime CalculateExpiryDate(DateTime startDate)
    {
        if (DurationValue <= 0)
        {
            throw new DomainException("Subscription plan duration must be greater than zero.");
        }

        try
        {
            return DurationType.Trim().ToUpperInvariant() switch
            {
                "DAY" => startDate.AddDays(DurationValue),
                "MONTH" => startDate.AddMonths(DurationValue),
                "YEAR" => startDate.AddYears(DurationValue),
                _ => throw new DomainException("Subscription plan duration type is invalid.")
            };
        }
        catch (ArgumentOutOfRangeException)
        {
            throw new DomainException("Subscription plan duration produces an invalid expiry date.");
        }
    }
}
