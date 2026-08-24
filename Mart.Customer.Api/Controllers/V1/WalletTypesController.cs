using Asp.Versioning;
using Mart.Customer.Api.Contracts.Wallets;
using Mart.Customer.Application.Wallets.Queries.GetWalletTypes;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mart.Customer.Api.Controllers.V1;

[ApiController]
[Authorize]
[ApiVersion(1.0)]
[Route("api/wallet-types")]
public sealed class WalletTypesController : ControllerBase
{
    private readonly ISender _sender;

    public WalletTypesController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet]
    public async Task<IActionResult> GetWalletTypes(
        [FromQuery] GetWalletTypesRequest request,
        CancellationToken cancellationToken)
    {
        var walletTypes = await _sender.Send(
            new GetWalletTypesQuery(request.ActiveOnly),
            cancellationToken);

        return Ok(walletTypes);
    }
}
