using Mart.Customer.Domain.Common;

namespace Mart.Customer.Domain.Referrals;

public sealed class CustomerReferral
{
    public const string WaitingStatus = "PENDING";
    public const string ActiveStatus = "ACTIVE";

    private CustomerReferral() { }

    public long CustomerReferralId { get; private set; }
    public long ReferrerCustomerId { get; private set; }
    public int ReferralConfigId { get; private set; }
    public decimal MinimumPurchaseAmount { get; private set; }
    public decimal ReferrerRewardPoint { get; private set; }
    public decimal ReferredCustomerRewardPoint { get; private set; }
    public string ReferredMobileNumber { get; private set; } = string.Empty;
    public string ReferralCode { get; private set; } = string.Empty;
    public string Status { get; private set; } = WaitingStatus;
    public long? ReferredCustomerId { get; private set; }
    public DateTime CreatedOn { get; private set; }
    public DateTime? OnboardedOn { get; private set; }
    public bool IsActive { get; private set; }

    public static CustomerReferral Create(
        long referrerCustomerId,
        int referralConfigId,
        decimal minimumPurchaseAmount,
        decimal referrerRewardPoint,
        decimal referredCustomerRewardPoint,
        string referredMobileNumber,
        string referralCode,
        DateTime createdOn)
    {
        var mobile = NormalizeMobile(referredMobileNumber);
        var code = referralCode.Trim().ToUpperInvariant();
        if (referrerCustomerId <= 0) throw new DomainException("Referrer customer is invalid.");
        if (referralConfigId <= 0) throw new DomainException("Referral configuration is invalid.");
        if (minimumPurchaseAmount < 0) throw new DomainException("Minimum purchase amount is invalid.");
        if (mobile.Length != 10 || !mobile.All(char.IsDigit))
            throw new DomainException("Enter a valid 10 digit mobile number.");
        if (string.IsNullOrWhiteSpace(code)) throw new DomainException("Referral code is required.");

        return new CustomerReferral
        {
            ReferrerCustomerId = referrerCustomerId,
            ReferralConfigId = referralConfigId,
            MinimumPurchaseAmount = minimumPurchaseAmount,
            ReferrerRewardPoint = referrerRewardPoint,
            ReferredCustomerRewardPoint = referredCustomerRewardPoint,
            ReferredMobileNumber = mobile,
            ReferralCode = code,
            Status = WaitingStatus,
            CreatedOn = createdOn,
            IsActive = true
        };
    }

    public void MarkOnboarded(long referredCustomerId, DateTime onboardedOn)
    {
        if (!IsActive || Status != WaitingStatus || ReferredCustomerId.HasValue)
            throw new DomainException("Referral code is inactive or has already been used.");
        if (referredCustomerId <= 0) throw new DomainException("Referred customer is invalid.");

        ReferredCustomerId = referredCustomerId;
        Status = ActiveStatus;
        OnboardedOn = onboardedOn;
        IsActive = false;
    }

    public static string NormalizeMobile(string value) =>
        new((value ?? string.Empty).Where(char.IsDigit).ToArray());
}
