using Mart.Customer.Domain.Common;

namespace Mart.Customer.Domain.Subscriptions;

public sealed class CustomerSubscription
{
    private const decimal MaximumWalletCreditAmount = 9999999999999999.99m;

    private CustomerSubscription()
    {
    }

    public long Id { get; private set; }
    public long CustomerId { get; private set; }
    public int SubscriptionPlanId { get; private set; }
    public decimal SubscriptionAmount { get; private set; }
    public DateTime StartDate { get; private set; }
    public DateTime ExpiryDate { get; private set; }
    public string Status { get; private set; } = string.Empty;
    public decimal ExtraPointPercentage { get; private set; }
    public decimal FeeToWalletPercentage { get; private set; }
    public int? WalletTypeId { get; private set; }
    public decimal? WalletCreditAmount { get; private set; }
    public bool? IsPointCreated { get; private set; }
    public long? PointReferenceId { get; private set; }
    public long? PaymentTransactionId { get; private set; }
    public DateTime CreatedOn { get; private set; }
    public int CreatedBy { get; private set; }

    public static CustomerSubscription Create(
        long customerId,
        SubscriptionPlan plan,
        decimal? walletCreditAmount,
        long? pointReferenceId,
        long? paymentTransactionId,
        long createdBy,
        DateTime createdOn)
    {
        if (customerId <= 0)
        {
            throw new DomainException("Customer id must be greater than zero.");
        }

        if (createdBy <= 0 || createdBy > int.MaxValue)
        {
            throw new DomainException("The authenticated user id is outside the supported range.");
        }

        if (plan.SubscriptionFee < 0 ||
            plan.ExtraPointPercentage is < 0 or > 100 ||
            plan.FeeToWalletPercentage is < 0 or > 100 ||
            plan.WalletTypeId <= 0)
        {
            throw new DomainException("Subscription plan configuration is invalid.");
        }

        ValidateWalletCreditAmount(walletCreditAmount);

        var startDate = createdOn.Date;

        return new CustomerSubscription
        {
            CustomerId = customerId,
            SubscriptionPlanId = plan.SubscriptionId,
            SubscriptionAmount = plan.SubscriptionFee,
            StartDate = startDate,
            ExpiryDate = plan.CalculateExpiryDate(startDate),
            Status = "ACTIVE",
            ExtraPointPercentage = plan.ExtraPointPercentage,
            FeeToWalletPercentage = plan.FeeToWalletPercentage,
            WalletTypeId = plan.WalletTypeId,
            IsPointCreated = true,
            WalletCreditAmount = walletCreditAmount,
            PointReferenceId = pointReferenceId,
            PaymentTransactionId = paymentTransactionId,
            CreatedBy = checked((int)createdBy),
            CreatedOn = createdOn
        };
    }

    public void UpdatePaymentDetails(
        bool? isPointCreated,
        decimal? walletCreditAmount,
        long? pointReferenceId,
        long? paymentTransactionId)
    {
        ValidateWalletCreditAmount(walletCreditAmount);

        IsPointCreated = isPointCreated;
        WalletCreditAmount = walletCreditAmount;
        PointReferenceId = pointReferenceId;
        PaymentTransactionId = paymentTransactionId;
    }

    private static void ValidateWalletCreditAmount(decimal? walletCreditAmount)
    {
        if (walletCreditAmount < 0)
        {
            throw new DomainException("Wallet credit amount cannot be negative.");
        }

        if (walletCreditAmount > MaximumWalletCreditAmount ||
            walletCreditAmount.HasValue && decimal.Round(walletCreditAmount.Value, 2) != walletCreditAmount.Value)
        {
            throw new DomainException("Wallet credit amount must fit decimal(18,2).");
        }
    }
}
