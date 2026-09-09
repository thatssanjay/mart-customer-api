using Mart.Customer.Application.Referrals.Dtos;
using Mart.Customer.Domain.Referrals;

namespace Mart.Customer.Application.Abstractions.Data;

public interface ICustomerReferralRepository
{
    Task<ReferralBenefitDto?> GetActiveBenefitAsync(DateTime currentDate, CancellationToken cancellationToken = default);
    Task<bool> ExistsForMobileAsync(long referrerCustomerId, string mobileNumber, CancellationToken cancellationToken = default);
    Task<bool> ReferralCodeExistsAsync(string referralCode, CancellationToken cancellationToken = default);
    Task<CustomerReferral?> GetAvailableByCodeAsync(string referralCode, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<CustomerReferralDto> Items, int TotalCount)> GetPagedAsync(
        long referrerCustomerId, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task AddAsync(CustomerReferral referral, CancellationToken cancellationToken = default);
}
