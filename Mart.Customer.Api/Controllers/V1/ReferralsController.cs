using Asp.Versioning;
using Mart.Customer.Api.Auth;
using Mart.Customer.Api.Contracts.Referrals;
using Mart.Customer.Application.Referrals.Commands.CreateCustomerReferral;
using Mart.Customer.Application.Referrals.Queries.GetCustomerReferrals;
using Mart.Customer.Application.Referrals.Queries.GetReferralBenefit;
using Mart.Customer.Application.Referrals.Services;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mart.Customer.Api.Controllers.V1;

[ApiController]
[ApiVersion(1.0)]
[Authorize]
[Route("api/v{version:apiVersion}/referrals")]
public sealed class ReferralsController(
    ISender sender,
    IMartUserContext currentUser,
    IReferralRewardService rewardService) : ControllerBase
{
    [Authorize(Policy = MartAuthorizationPolicies.MobileCustomer)]
    [HttpGet("benefit")]
    public async Task<IActionResult> GetBenefit(CancellationToken cancellationToken)
    {
        var benefit = await sender.Send(new GetReferralBenefitQuery(), cancellationToken);
        return benefit is null
            ? NoContent()
            : Ok(benefit);
    }

    [Authorize(Policy = MartAuthorizationPolicies.MobileCustomer)]
    [HttpGet("mine")]
    public async Task<IActionResult> GetMine(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var result = await sender.Send(
            new GetCustomerReferralsQuery(currentUser.UserId, pageNumber, pageSize),
            cancellationToken);
        return Ok(result);
    }

    [Authorize(Policy = MartAuthorizationPolicies.MobileCustomer)]
    [HttpPost]
    public async Task<IActionResult> Create(
        CreateCustomerReferralRequest request,
        CancellationToken cancellationToken)
    {
        var referral = await sender.Send(
            new CreateCustomerReferralCommand(
                currentUser.UserId,
                request.MobileNumber ?? string.Empty,
                request.ReferralConfigId),
            cancellationToken);
        return Created(string.Empty, referral);
    }

    //[Authorize(Policy = MartAuthorizationPolicies.MartAdmin)]
    [HttpPost("process-rewards")]
    public async Task<IActionResult> ProcessRewards(CancellationToken cancellationToken)
    {
        var result = await rewardService.ProcessAsync(currentUser.UserId, cancellationToken);
        return Ok(result);
    }
}
