namespace Mart.Customer.Application.Referrals.Dtos;

public sealed record ReferralBenefitDto(
    int ReferralConfigId,
    decimal MinimumPurchaseAmount,
    decimal ReferrerRewardPoint,
    decimal ReferredCustomerRewardPoint,
    int WalletTypeId,
    string WalletName,
    string WalletCode);

public sealed record CustomerReferralDto(
    long CustomerReferralId,
    string ReferredMobileNumber,
    string ReferralCode,
    string Status,
    DateTime CreatedOn,
    DateTime? OnboardedOn);

public sealed record CreatedCustomerReferralDto(
    long CustomerReferralId,
    string ReferredMobileNumber,
    string ReferralCode,
    string Status,
    DateTime CreatedOn);

public sealed record ProcessReferralRewardsResultDto(
    int ActiveReferralCount,
    int QualifiedReferralCount,
    decimal ReferrerPointsCredited,
    decimal ReferredCustomerPointsCredited,
    DateTime ProcessedOn);
