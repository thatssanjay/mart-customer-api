namespace Mart.Customer.Domain.Referrals;

public sealed class ReferralConfiguration
{
    private ReferralConfiguration() { }

    public int Id { get; private set; }
    public decimal ReferrerRewardPoint { get; private set; }
    public int RewardWalletTypeId { get; private set; }
    public DateTime StartDate { get; private set; }
    public DateTime? EndDate { get; private set; }
    public bool IsActive { get; private set; }
}
