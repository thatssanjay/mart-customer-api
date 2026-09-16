using Asp.Versioning;
using Mart.Customer.Api.Auth;
using Mart.Customer.Api.Contracts.Promotions;
using Mart.Customer.Application.Auth.Queries.GetMartUserAccessScope;
using Mart.Customer.Application.Promotions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mart.Customer.Api.Controllers.V1;

[ApiController]
[ApiVersion(1.0)]
[Authorize(Policy = MartAuthorizationPolicies.FranchiseAdmin)]
[Route("api/v{version:apiVersion}/store-promotions")]
public sealed class StorePromotionsController(
    IStorePromotionService promotions,
    ISender sender,
    IMartUserContext currentUser) : ControllerBase
{
    [HttpGet("orders/search")]
    public async Task<IActionResult> SearchTodayOrders(
        [FromQuery] string? invoiceNumber,
        [FromQuery] string? mobileNumber,
        CancellationToken cancellationToken)
    {
        var scope = await GetScopeAsync(cancellationToken);
        return Ok(await promotions.SearchTodayOrdersAsync(
            scope.FranchiseId,
            scope.StoreId,
            invoiceNumber,
            mobileNumber,
            cancellationToken));
    }

    [HttpGet("active")]
    public async Task<IActionResult> GetActive(CancellationToken cancellationToken)
    {
        var scope = await GetScopeAsync(cancellationToken);
        return Ok(await promotions.GetActivePromotionsAsync(
            scope.FranchiseId,
            scope.StoreId,
            cancellationToken));
    }

    [HttpPost("allocate")]
    public async Task<IActionResult> Allocate(
        AllocateStorePromotionRequest request,
        CancellationToken cancellationToken)
    {
        var scope = await GetScopeAsync(cancellationToken);
        return Ok(await promotions.AllocateAsync(
            currentUser.UserId,
            scope.FranchiseId,
            scope.StoreId,
            request.OrderId,
            request.PromoCode ?? string.Empty,
            currentUser.UserName ?? currentUser.UserId.ToString(),
            cancellationToken));
    }

    private Task<Mart.Customer.Application.Auth.Dtos.MartUserAccessScopeDto> GetScopeAsync(
        CancellationToken cancellationToken) =>
        sender.Send(new GetMartUserAccessScopeQuery(currentUser.UserId), cancellationToken);
}
