using Asp.Versioning;
using Mart.Customer.Api.Contracts.Wallets;
using Mart.Customer.Application.Wallets.Queries.GetWalletTransactionByNumber;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mart.Customer.Api.Controllers.V1;

[ApiController]
[Authorize]
[ApiVersion(1.0)]
[Route("api/wallet-transactions")]
public sealed class WalletTransactionsController : ControllerBase
{
    private readonly ISender _sender;

    public WalletTransactionsController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet("{transactionNumber}")]
    public async Task<IActionResult> GetByTransactionNumber(
        [FromRoute] GetWalletTransactionByNumberRequest request,
        CancellationToken cancellationToken)
    {
        var transaction = await _sender.Send(
            new GetWalletTransactionByNumberQuery(request.TransactionNumber),
            cancellationToken);

        return transaction is null
            ? NotFound(new { message = "Wallet transaction not found." })
            : Ok(transaction);
    }
}
