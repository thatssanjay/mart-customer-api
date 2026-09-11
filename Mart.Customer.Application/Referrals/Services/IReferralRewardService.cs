using Mart.Customer.Application.Referrals.Dtos;

namespace Mart.Customer.Application.Referrals.Services;

public interface IReferralRewardService
{
    Task<ProcessReferralRewardsResultDto> ProcessAsync(
        long processedBy,
        CancellationToken cancellationToken = default);
}
