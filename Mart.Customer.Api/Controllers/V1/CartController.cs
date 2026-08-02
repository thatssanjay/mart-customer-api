using Asp.Versioning;
using Mart.Customer.Api.Auth;
using Mart.Customer.Api.Contracts.Carts;
using Mart.Customer.Application.Carts.Commands.AddCartItem;
using Mart.Customer.Application.Carts.Commands.ChangeCartStatus;
using Mart.Customer.Application.Carts.Commands.CreateCart;
using Mart.Customer.Application.Carts.Commands.DeleteCart;
using Mart.Customer.Application.Carts.Commands.DeleteCartItemsByProduct;
using Mart.Customer.Application.Carts.Commands.UpdateCartItem;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mart.Customer.Api.Controllers.V1;

[ApiController]
[Authorize]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/carts")]
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
        var cart = await _sender.Send(
            new CreateCartCommand(
                request.CustomerId,
                _currentUser.FranchiseId,
                _currentUser.StoreId,
                _currentUser.UserId),
            cancellationToken);

        return Created(string.Empty, cart);
    }

    [HttpDelete("{cartNumber}")]
    public async Task<IActionResult> DeleteCart(
        string cartNumber,
        CancellationToken cancellationToken)
    {
        var deleted = await _sender.Send(
            new DeleteCartCommand(
                cartNumber,
                _currentUser.FranchiseId,
                _currentUser.StoreId),
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
        var item = await _sender.Send(
            new AddCartItemCommand(
                cartNumber,
                _currentUser.FranchiseId,
                _currentUser.StoreId,
                _currentUser.UserId,
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
        var cart = await _sender.Send(
            new ChangeCartStatusCommand(
                cartNumber,
                request.CartStatus,
                _currentUser.FranchiseId,
                _currentUser.StoreId),
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
        var item = await _sender.Send(
            new UpdateCartItemCommand(
                cartNumber,
                customerCartItemId,
                _currentUser.FranchiseId,
                _currentUser.StoreId,
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
        var deleted = await _sender.Send(
            new DeleteCartItemsByProductCommand(
                cartNumber,
                productId,
                _currentUser.FranchiseId,
                _currentUser.StoreId),
            cancellationToken);

        return deleted
            ? NoContent()
            : NotFound(new { message = "Cart or product item not found." });
    }
}
