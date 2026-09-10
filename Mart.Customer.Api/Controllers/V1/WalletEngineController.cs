using Asp.Versioning;
using Mart.Customer.Api.Auth;
using Mart.Customer.Application.Auth.Queries.GetMartUserAccessScope;
using Mart.Customer.Application.Orders.Commands.MarkOrderPointsAwarded;
using Mart.Customer.Application.Orders.Dtos;
using Mart.Customer.Application.Orders.Queries.GetPendingPointsOrders;
using Mart.Customer.Application.Wallets.Engine;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mart.Customer.Api.Controllers.V1;

[ApiController]
[Authorize]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/wallet-engine/orders")]
public sealed class WalletEngineController(
    IWalletEngineService walletEngine,
    ISender sender,
    IMartUserContext currentUser) : ControllerBase
{
  //  [Authorize(Policy = MartAuthorizationPolicies.PendingPointsMartAdmin)]
    [HttpGet("pending-points")]
    public async Task<IActionResult> GetPendingPoints(CancellationToken cancellationToken)
    {
        var orders = await sender.Send(
            new GetPendingPointsOrdersQuery(),
            cancellationToken);

        return Ok(orders);
    }

   // [Authorize(Policy = MartAuthorizationPolicies.MartAdmin)]
    [HttpPut("{orderId:long}/points-awarded")]
    public async Task<IActionResult> MarkPointsAwarded(
        long orderId,
        CancellationToken cancellationToken)
    {
        var scope = await sender.Send(
            new GetMartUserAccessScopeQuery(currentUser.UserId),
            cancellationToken);
        var result = await sender.Send(
            new MarkOrderPointsAwardedCommand(orderId, scope.FranchiseId, scope.StoreId),
            cancellationToken);

        return result.Status switch
        {
            MarkOrderPointsAwardedStatus.Updated => Ok(result.Order),
            MarkOrderPointsAwardedStatus.Forbidden => Forbid(),
            MarkOrderPointsAwardedStatus.AlreadyAwarded => ValidationProblem(
                new ValidationProblemDetails(new Dictionary<string, string[]>
                {
                    ["orderId"] = ["Points have already been awarded for this order."]
                })),
            _ => NotFound(new { message = "Order not found." })
        };
    }

   // [Authorize(Policy = MartAuthorizationPolicies.MartAdmin)]
    [HttpPost("{orderId:long}/credit")]
    public async Task<ActionResult<OrderWalletCreditResult>> Credit(
        long orderId, CancellationToken cancellationToken)
    {
        var scope = await sender.Send(
            new GetMartUserAccessScopeQuery(currentUser.UserId),
            cancellationToken);
        var access = new OrderWalletAccess(currentUser.UserId, scope.FranchiseId, scope.StoreId);
        var result = await walletEngine.CreditPaidOrderAsync(orderId, access, cancellationToken);
        return Ok(result);
    }
}
