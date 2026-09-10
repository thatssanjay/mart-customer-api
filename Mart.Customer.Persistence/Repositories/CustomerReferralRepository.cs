using Mart.Customer.Application.Abstractions.Data;
using Mart.Customer.Application.Referrals.Dtos;
using Mart.Customer.Domain.Referrals;
using Microsoft.EntityFrameworkCore;

namespace Mart.Customer.Persistence.Repositories;

internal sealed class CustomerReferralRepository(ApplicationDbContext dbContext) : ICustomerReferralRepository
{
    public Task<ReferralBenefitDto?> GetActiveBenefitAsync(
        DateTime currentDate,
        CancellationToken cancellationToken = default)
    {
        return (from configuration in dbContext.ReferralConfigurations.AsNoTracking()
                join wallet in dbContext.WalletTypes.AsNoTracking()
                    on configuration.RewardWalletTypeId equals wallet.Id
                where configuration.IsActive &&
                    configuration.StartDate <= currentDate &&
                    (!configuration.EndDate.HasValue || configuration.EndDate >= currentDate)
                orderby configuration.StartDate descending, configuration.Id descending
                select new ReferralBenefitDto(
                    configuration.Id,
                    configuration.MinimumPurchaseAmount,
                    configuration.ReferrerRewardPoint,
                    configuration.ReferredCustomerRewardPoint,
                    wallet.Id,
                    wallet.Name,
                    wallet.Code))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<ReferralConfiguration?> GetActiveConfigurationAsync(
        int referralConfigId,
        DateTime currentDate,
        CancellationToken cancellationToken = default) =>
        dbContext.ReferralConfigurations.AsNoTracking().SingleOrDefaultAsync(
            configuration => configuration.Id == referralConfigId &&
                configuration.IsActive &&
                configuration.StartDate <= currentDate &&
                (!configuration.EndDate.HasValue || configuration.EndDate >= currentDate),
            cancellationToken);

    public Task<bool> ExistsForMobileAsync(
        long referrerCustomerId,
        string mobileNumber,
        CancellationToken cancellationToken = default)
    {
        var mobile = CustomerReferral.NormalizeMobile(mobileNumber);
        return dbContext.CustomerReferrals.AsNoTracking().AnyAsync(
            referral => referral.ReferrerCustomerId == referrerCustomerId &&
                referral.ReferredMobileNumber == mobile,
            cancellationToken);
    }

    public Task<bool> ReferralCodeExistsAsync(string referralCode, CancellationToken cancellationToken = default)
    {
        var code = referralCode.Trim().ToUpperInvariant();
        return dbContext.CustomerReferrals.AsNoTracking()
            .AnyAsync(referral => referral.ReferralCode == code, cancellationToken);
    }

    public Task<CustomerReferral?> GetAvailableByCodeAsync(
        string referralCode,
        CancellationToken cancellationToken = default)
    {
        var code = referralCode.Trim().ToUpperInvariant();
        return dbContext.CustomerReferrals.SingleOrDefaultAsync(
            referral => referral.ReferralCode == code &&
                referral.Status == CustomerReferral.WaitingStatus &&
                referral.ReferredCustomerId == null,
            cancellationToken);
    }

    public async Task<(IReadOnlyList<CustomerReferralDto> Items, int TotalCount)> GetPagedAsync(
        long referrerCustomerId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.CustomerReferrals.AsNoTracking()
            .Where(referral => referral.ReferrerCustomerId == referrerCustomerId);
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(referral => referral.CreatedOn)
            .ThenByDescending(referral => referral.CustomerReferralId)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(referral => new CustomerReferralDto(
                referral.CustomerReferralId,
                referral.ReferredMobileNumber,
                referral.ReferralCode,
                referral.Status,
                referral.CreatedOn,
                referral.OnboardedOn))
            .ToListAsync(cancellationToken);
        return (items, totalCount);
    }

    public Task AddAsync(CustomerReferral referral, CancellationToken cancellationToken = default) =>
        dbContext.CustomerReferrals.AddAsync(referral, cancellationToken).AsTask();
}
