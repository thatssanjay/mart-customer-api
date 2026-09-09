using Mart.Customer.Application.Referrals.Dtos;
using MediatR;

namespace Mart.Customer.Application.Referrals.Commands.CreateCustomerReferral;

public sealed record CreateCustomerReferralCommand(long ReferrerCustomerId, string ReferredMobileNumber)
    : IRequest<CreatedCustomerReferralDto>;
