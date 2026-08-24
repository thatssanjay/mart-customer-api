using Asp.Versioning;
using Mart.Customer.Api.Auth;
using Mart.Customer.Api.Contracts.Carts;
using Mart.Customer.Application.Auth.Dtos;
using Mart.Customer.Application.Auth.Queries.GetMartUserAccessScope;
using Mart.Customer.Application.Carts.Commands.AddCartItem;
using Mart.Customer.Application.Carts.Commands.CancelCart;
using Mart.Customer.Application.Carts.Commands.ChangeCartStatus;
using Mart.Customer.Application.Carts.Commands.CreateCart;
using Mart.Customer.Application.Carts.Commands.DeleteCart;
using Mart.Customer.Application.Carts.Commands.DeleteCartItemsByProduct;
using Mart.Customer.Application.Carts.Commands.UpdateCartItem;
using Mart.Customer.Application.Carts.Queries.GetCustomerCarts;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Mart.Customer.Api.Controllers.V1;

[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/inventory/carts")]
public sealed class CartController : ControllerBase
{
    private readonly ISender _sender;
    private readonly IMartUserContext _currentUser;

    public CartController(ISender sender, IMartUserContext currentUser)
    {
        _sender = sender;
        _currentUser = currentUser;
    }

    [HttpPost]
    public async Task<IActionResult> CreateCart(
        CreateCartRequest request,
        CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId;
        var access = await GetAccessScopeAsync(userId, cancellationToken);
        var cart = await _sender.Send(
            new CreateCartCommand(
                request.CustomerId,
                access.FranchiseId,
                access.StoreId,
                userId),
            cancellationToken);

        return Created(string.Empty, cart);
    }

    [HttpGet]
    public async Task<IActionResult> GetCarts(
        [FromQuery] long customerId,
        [FromQuery] string? cartStatus,
        CancellationToken cancellationToken)
    {
        var access = await GetAccessScopeAsync(_currentUser.UserId, cancellationToken);
        var carts = await _sender.Send(
            new GetCustomerCartsQuery(
                customerId,
                cartStatus,
                access.FranchiseId,
                access.StoreId),
            cancellationToken);

        return Ok(carts);
    }

    [HttpDelete("{cartNumber}")]
    public async Task<IActionResult> DeleteCart(
        string cartNumber,
        CancellationToken cancellationToken)
    {
        var access = await GetAccessScopeAsync(_currentUser.UserId, cancellationToken);
        var deleted = await _sender.Send(
            new DeleteCartCommand(
                cartNumber,
                access.FranchiseId,
                access.StoreId),
            cancellationToken);

        return deleted
            ? NoContent()
            : NotFound(new { message = "Cart not found." });
    }

    [HttpPost("{cartNumber}/items")]
    public async Task<IActionResult> AddCartItem(
        string cartNumber,
        CreateCartItemRequest request,
        CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId;
        var access = await GetAccessScopeAsync(userId, cancellationToken);
        var item = await _sender.Send(
            new AddCartItemCommand(
                cartNumber,
                access.FranchiseId,
                access.StoreId,
                userId,
                request.ProductId,
                request.ProductNameSnapshot,
                request.Quantity,
                request.UnitPrice,
                request.MRP,
                request.DiscountAmount,
                request.GSTPercent),
            cancellationToken);

        return item is null
            ? NotFound(new { message = "Cart not found." })
            : Created(string.Empty, item);
    }

    [HttpPatch("{cartNumber}/status")]
    public async Task<IActionResult> ChangeCartStatus(
        string cartNumber,
        ChangeCartStatusRequest request,
        CancellationToken cancellationToken)
    {
        var access = await GetAccessScopeAsync(_currentUser.UserId, cancellationToken);
        var cart = await _sender.Send(
            new ChangeCartStatusCommand(
                cartNumber,
                request.CartStatus,
                access.FranchiseId,
                access.StoreId),
            cancellationToken);

        return cart is null
            ? NotFound(new { message = "Cart not found." })
            : Ok(cart);
    }

    [HttpPost("~/api/v{version:apiVersion}/carts/{cartId:long}/cancel")]
    public async Task<IActionResult> CancelCart(
        long cartId,
        CancelCartRequest request,
        CancellationToken cancellationToken)
    {
        var access = await GetAccessScopeAsync(_currentUser.UserId, cancellationToken);
        var cart = await _sender.Send(
            new CancelCartCommand(
                cartId,
                request.Remarks,
                access.FranchiseId,
                access.StoreId),
            cancellationToken);

        return cart is null
            ? NotFound(new { message = "Cart not found." })
            : Ok(cart);
    }

    [HttpPut("{cartNumber}/items/{customerCartItemId:long}")]
    public async Task<IActionResult> UpdateCartItem(
        string cartNumber,
        long customerCartItemId,
        UpdateCartItemRequest request,
        CancellationToken cancellationToken)
    {
        var access = await GetAccessScopeAsync(_currentUser.UserId, cancellationToken);
        var item = await _sender.Send(
            new UpdateCartItemCommand(
                cartNumber,
                customerCartItemId,
                access.FranchiseId,
                access.StoreId,
                request.ProductNameSnapshot,
                request.Quantity,
                request.UnitPrice,
                request.MRP,
                request.DiscountAmount,
                request.GSTPercent),
            cancellationToken);

        return item is null
            ? NotFound(new { message = "Cart or cart item not found." })
            : Ok(item);
    }

    [HttpDelete("{cartNumber}/items/{productId:long}")]
    public async Task<IActionResult> DeleteCartItemsByProduct(
        string cartNumber,
        long productId,
        CancellationToken cancellationToken)
    {
        var access = await GetAccessScopeAsync(_currentUser.UserId, cancellationToken);
        var deleted = await _sender.Send(
            new DeleteCartItemsByProductCommand(
                cartNumber,
                productId,
                access.FranchiseId,
                access.StoreId),
            cancellationToken);

        return deleted
            ? NoContent()
            : NotFound(new { message = "Cart or product item not found." });
    }

    private Task<MartUserAccessScopeDto> GetAccessScopeAsync(
        long userId,
        CancellationToken cancellationToken)
    {
        return _sender.Send(new GetMartUserAccessScopeQuery(userId), cancellationToken);
    }
}
