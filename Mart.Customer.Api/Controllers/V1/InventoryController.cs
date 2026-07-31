using Asp.Versioning;
using Mart.Customer.Application.Inventory.Queries.SearchProducts;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Mart.Customer.Api.Controllers.V1;

[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/inventory")]
public sealed class InventoryController : ControllerBase
{
    private readonly ISender _sender;

    public InventoryController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet("products")]
    public async Task<IActionResult> GetProducts(
        [FromQuery] string? search,
        CancellationToken cancellationToken)
    {
        var products = await _sender.Send(new SearchProductsQuery(search), cancellationToken);
        return Ok(products);
    }
}
