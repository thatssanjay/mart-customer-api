using Asp.Versioning;
using Mart.Customer.Api.Auth;
using Mart.Customer.Api.Contracts.Inventory;
using Mart.Customer.Application.Auth.Queries.GetMartUserAccessScope;
using Mart.Customer.Application.Inventory.Commands.StockIn;
using Mart.Customer.Application.Inventory.Commands.StockOut;
using Mart.Customer.Application.Inventory.Queries.GetStockProduct;
using Mart.Customer.Application.Inventory.Queries.GetStockMovements;
using Mart.Customer.Application.Inventory.Queries.GetLowStockProducts;
using Mart.Customer.Application.Inventory.Queries.SearchProducts;
using Mart.Customer.Application.Inventory.Queries.SearchStockProducts;
using Mart.Customer.Application.Inventory.Services;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Mart.Customer.Api.Controllers.V1;

[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/inventory")]
public sealed class InventoryController : ControllerBase
{
    private readonly ISender _sender;
    private readonly IMartUserContext _currentUser;
    private readonly IInventoryStockService _inventoryStockService;

    public InventoryController(
        ISender sender,
        IMartUserContext currentUser,
        IInventoryStockService inventoryStockService)
    {
        _sender = sender;
        _currentUser = currentUser;
        _inventoryStockService = inventoryStockService;
    }

    [HttpGet("products")]
    public async Task<IActionResult> GetProducts(
        [FromQuery] string? search,
        CancellationToken cancellationToken)
    {
        var products = await _sender.Send(new SearchProductsQuery(search), cancellationToken);
        return Ok(products);
    }

    [HttpGet("stock/products")]
    public async Task<IActionResult> GetStockProducts(
        [FromQuery] string? search,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var access = await _sender.Send(
            new GetMartUserAccessScopeQuery(_currentUser.UserId),
            cancellationToken);
        var products = await _sender.Send(
            new SearchStockProductsQuery(
                access.FranchiseId,
                access.StoreId,
                search,
                pageNumber,
                pageSize),
            cancellationToken);

        return Ok(products);
    }

    [HttpGet("stock/products/{productId:long}")]
    public async Task<IActionResult> GetStockProduct(
        long productId,
        CancellationToken cancellationToken)
    {
        var access = await _sender.Send(
            new GetMartUserAccessScopeQuery(_currentUser.UserId),
            cancellationToken);
        var product = await _sender.Send(
            new GetStockProductQuery(productId, access.FranchiseId, access.StoreId),
            cancellationToken);

        return product is null
            ? NotFound(new { message = "Active stock-managed product not found." })
            : Ok(product);
    }

    [HttpGet("stock/movements")]
    public async Task<IActionResult> GetStockMovements(
        [FromQuery] long? productId,
        [FromQuery] string? movementType,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var access = await _sender.Send(
            new GetMartUserAccessScopeQuery(_currentUser.UserId),
            cancellationToken);
        var movements = await _sender.Send(
            new GetStockMovementsQuery(
                access.FranchiseId,
                access.StoreId,
                productId,
                movementType,
                fromDate,
                toDate,
                pageNumber,
                pageSize),
            cancellationToken);

        return Ok(movements);
    }

    [HttpGet("stock/low-stock")]
    public async Task<IActionResult> GetLowStockProducts(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var access = await _sender.Send(
            new GetMartUserAccessScopeQuery(_currentUser.UserId),
            cancellationToken);
        var products = await _sender.Send(
            new GetLowStockProductsQuery(
                access.FranchiseId,
                access.StoreId,
                pageNumber,
                pageSize),
            cancellationToken);

        return Ok(products);
    }

    [HttpPost("stock/in")]
    public async Task<IActionResult> StockIn(
        [FromBody] StockInRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _inventoryStockService.StockInAsync(
            new StockInCommand(
                _currentUser.UserId,
                request.ProductId,
                request.Quantity,
                request.MovementType,
                request.ReferenceType,
                request.ReferenceId,
                request.BatchNumber,
                request.ManufacturingDate,
                request.ExpiryDate,
                request.PurchasePrice,
                request.SellingPrice,
                request.Mrp,
                request.Remarks,
                request.AdjustmentReason),
            cancellationToken);

        return Ok(result);
    }

    [HttpPost("stock/out")]
    public async Task<IActionResult> StockOut(
        [FromBody] StockOutRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _inventoryStockService.StockOutAsync(
            new StockOutCommand(
                _currentUser.UserId,
                request.ProductId,
                request.Quantity,
                request.MovementType,
                request.BatchNumber,
                request.Reason,
                request.Remarks),
            cancellationToken);

        return Ok(result);
    }
}
