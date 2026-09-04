using Asp.Versioning;
using Mart.Customer.Api.Auth;
using Mart.Customer.Application.Auth.Queries.GetMartUserAccessScope;
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
    [HttpPost("{orderId:long}/credit")]
    public async Task<ActionResult<OrderWalletCreditResult>> Credit(
        long orderId, CancellationToken cancellationToken)
    {
        OrderWalletAccess access;
        if (string.Equals(currentUser.LoginType, "customer", StringComparison.OrdinalIgnoreCase))
        {
            access = new OrderWalletAccess(currentUser.UserId, 0, 0, currentUser.UserId);
        }
        else
        {
            var scope = await sender.Send(new GetMartUserAccessScopeQuery(currentUser.UserId), cancellationToken);
            access = new OrderWalletAccess(currentUser.UserId, scope.FranchiseId, scope.StoreId);
        }
        var result = await walletEngine.CreditPaidOrderAsync(orderId, access, cancellationToken);
        return Ok(result);
    }
}
