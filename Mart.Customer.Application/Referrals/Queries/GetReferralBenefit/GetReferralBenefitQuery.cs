using Mart.Customer.Application.Referrals.Dtos;
using MediatR;

namespace Mart.Customer.Application.Referrals.Queries.GetReferralBenefit;

public sealed record GetReferralBenefitQuery : IRequest<ReferralBenefitDto?>;
