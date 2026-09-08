using Asp.Versioning;
using Mart.Customer.Api.Auth;
using Mart.Customer.Api.Contracts.Subscriptions;
using Mart.Customer.Application.Subscriptions.Commands.CreateCustomerSubscription;
using Mart.Customer.Application.Subscriptions.Commands.UpdateSubscriptionPaymentDetails;
using Mart.Customer.Application.Subscriptions.Queries.GetActiveSubscriptionPlans;
using Mart.Customer.Application.Subscriptions.Queries.GetCustomerSubscriptions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mart.Customer.Api.Controllers.V1;

[ApiController]
[Authorize]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/subscriptions")]
public sealed class SubscriptionController : ControllerBase
{
    private readonly ISender _sender;
    private readonly IMartUserContext _currentUser;

    public SubscriptionController(ISender sender, IMartUserContext currentUser)
    {
        _sender = sender;
        _currentUser = currentUser;
    }

    [HttpGet("active-plans")]
    public async Task<IActionResult> GetActivePlans(CancellationToken cancellationToken)
    {
        var plans = await _sender.Send(new GetActiveSubscriptionPlansQuery(), cancellationToken);
        return Ok(plans);
    }

    [HttpPost("CreateCustomerSubscription")]
    public async Task<IActionResult> CreateCustomerSubscription(
        CreateCustomerSubscriptionRequest request,
        CancellationToken cancellationToken)
    {
        var subscription = await _sender.Send(
            new CreateCustomerSubscriptionCommand(
                request.CustomerId,
                request.SubscriptionPlanId,
                request.WalletCreditAmount,
                request.PointReferenceId,
                request.PaymentTransactionId,
                _currentUser.UserId),
            cancellationToken);

        return Created(string.Empty, subscription);
    }

    [Authorize(Policy = MartAuthorizationPolicies.MobileCustomer)]
    [HttpPost("subscribe")]
    public async Task<IActionResult> Subscribe(
        SubscribeRequest request,
        CancellationToken cancellationToken)
    {
        var customerId = _currentUser.UserId;
        var subscription = await _sender.Send(
            new CreateCustomerSubscriptionCommand(
                customerId, request.SubscriptionPlanId, null, null, null, customerId),
            cancellationToken);

        return Created(string.Empty, subscription);
    }

    [Authorize(Policy = MartAuthorizationPolicies.MobileCustomer)]
    [HttpGet("my-subscriptions")]
    public async Task<IActionResult> GetMySubscriptions(CancellationToken cancellationToken)
    {
        var subscriptions = await _sender.Send(
            new GetCustomerSubscriptionsQuery(_currentUser.UserId), cancellationToken);
        return Ok(subscriptions);
    }

    [HttpPut("UpdateSubscriptionPaymentDetails/{id:long}/customers/{customerId:long}")]
    public async Task<IActionResult> UpdateSubscriptionPaymentDetails(
        long id,
        long customerId,
        UpdateSubscriptionPaymentDetailsRequest request,
        CancellationToken cancellationToken)
    {
        var subscription = await _sender.Send(
            new UpdateSubscriptionPaymentDetailsCommand(
                id,
                customerId,
                request.IsPointCreated,
                request.WalletCreditAmount,
                request.PointReferenceId,
                request.PaymentTransactionId),
            cancellationToken);

        return subscription is null
            ? NotFound(new { message = "Customer subscription not found." })
            : Ok(subscription);
    }
}
