using Asp.Versioning;
using Mart.Customer.Application.Cashback.Queries.GetActiveCashbackSettings;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Mart.Customer.Api.Controllers.V1;

[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/cashback-settings")]
public sealed class CashbackSettingsController : ControllerBase
{
    private readonly ISender _sender;

    public CashbackSettingsController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet]
    public async Task<IActionResult> GetActiveCashbackSettings(CancellationToken cancellationToken)
    {
        var settings = await _sender.Send(new GetActiveCashbackSettingsQuery(), cancellationToken);
        return Ok(settings);
    }
}
