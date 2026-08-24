using Asp.Versioning;
using Mart.Customer.Api.Auth;
using Mart.Customer.Api.Contracts.Subscriptions;
using Mart.Customer.Application.Subscriptions.Commands.CreateCustomerSubscription;
using Mart.Customer.Application.Subscriptions.Commands.UpdateSubscriptionPaymentDetails;
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
