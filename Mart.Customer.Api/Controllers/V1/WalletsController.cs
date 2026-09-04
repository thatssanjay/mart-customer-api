using Mart.Customer.Api.Auth;
using System.Security.Claims;
using Asp.Versioning;
using Mart.Customer.Api.Contracts.Wallets;
using Mart.Customer.Application.Wallets.Commands.CreditWallet;
using Mart.Customer.Application.Wallets.Commands.RedeemWallet;
using Mart.Customer.Application.Wallets.Commands.RefundWallet;
using Mart.Customer.Application.Wallets.Queries.PreviewWalletRedemption;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mart.Customer.Api.Controllers.V1;

[ApiController]
[Authorize]
[ApiVersion(1.0)]
[Route("api/wallets")]
public sealed class WalletsController : ControllerBase
{
    private readonly ISender _sender;

    public WalletsController(ISender sender)
    {
        _sender = sender;
    }

    [HttpPost("credit")]
    public async Task<IActionResult> Credit(
        CreditWalletRequest request,
        CancellationToken cancellationToken)
    {
        var createdBy = User.Identity?.Name
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? "authenticated-user";
        var result = await _sender.Send(
            new CreditWalletCommand(
                request.CustomerId,
                request.WalletTypeId,
                request.Amount,
                request.ReferenceType,
                request.ReferenceId,
                request.Remarks,
                request.ExpiryDate,
                createdBy) { StoreId = WalletStoreContext.GetStoreId(HttpContext) },
            cancellationToken);

        return Ok(result);
    }

    [HttpPost("redeem-preview")]
    public async Task<IActionResult> PreviewRedemption(
        RedeemPreviewRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new PreviewWalletRedemptionQuery(
                request.CustomerId,
                request.WalletTypeId,
                request.Amount) { StoreId = WalletStoreContext.GetStoreId(HttpContext) },
            cancellationToken);

        return Ok(result);
    }

    [HttpPost("redeem")]
    public async Task<IActionResult> Redeem(
        RedeemWalletRequest request,
        CancellationToken cancellationToken)
    {
        var createdBy = User.Identity?.Name
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? "authenticated-user";
        var result = await _sender.Send(
            new RedeemWalletCommand(
                request.CustomerId,
                request.WalletTypeId,
                request.Amount,
                request.ReferenceType,
                request.ReferenceId,
                request.Remarks,
                createdBy) { StoreId = WalletStoreContext.GetStoreId(HttpContext) },
            cancellationToken);

        return Ok(result);
    }

    [HttpPost("refund")]
    public async Task<IActionResult> Refund(
        RefundWalletRequest request,
        CancellationToken cancellationToken)
    {
        var createdBy = User.Identity?.Name
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? "authenticated-user";
        var result = await _sender.Send(
            new RefundWalletCommand(
                request.CustomerId,
                request.WalletTypeId,
                request.OriginalTransactionNumber,
                request.Amount,
                request.Remarks,
                createdBy) { StoreId = WalletStoreContext.GetStoreId(HttpContext) },
            cancellationToken);

        return Ok(result);
    }
}
