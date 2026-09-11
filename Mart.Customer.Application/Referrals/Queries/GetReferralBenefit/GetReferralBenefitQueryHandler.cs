using Mart.Customer.Application.Abstractions.Data;
using Mart.Customer.Application.Referrals.Dtos;
using MediatR;

namespace Mart.Customer.Application.Referrals.Queries.GetReferralBenefit;

public sealed class GetReferralBenefitQueryHandler(ICustomerReferralRepository referrals)
    : IRequestHandler<GetReferralBenefitQuery, ReferralBenefitDto?>
{
    public Task<ReferralBenefitDto?> Handle(GetReferralBenefitQuery request, CancellationToken cancellationToken) =>
        referrals.GetActiveBenefitAsync(cancellationToken);
}
