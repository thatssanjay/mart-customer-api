using Mart.Customer.Application.Abstractions.Data;
using Mart.Customer.Application.Carts.Dtos;
using Mart.Customer.Application.Inventory.Services;
using MediatR;

namespace Mart.Customer.Application.Carts.Commands.UpdateCartItem;

public sealed class UpdateCartItemCommandHandler : IRequestHandler<UpdateCartItemCommand, CreatedCartItemDto?>
{
    private readonly ICustomerCartRepository _cartRepository;
    private readonly IInventoryStockService _inventoryStockService;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateCartItemCommandHandler(
        ICustomerCartRepository cartRepository,
        IInventoryStockService inventoryStockService,
        IUnitOfWork unitOfWork)
    {
        _cartRepository = cartRepository;
        _inventoryStockService = inventoryStockService;
        _unitOfWork = unitOfWork;
    }

    public async Task<CreatedCartItemDto?> Handle(
        UpdateCartItemCommand request,
        CancellationToken cancellationToken)
    {
        var cart = await _cartRepository.GetByCartNumberAsync(
            request.CartNumber.Trim(),
            request.FranchiseId,
            request.MartStoreId,
            cancellationToken);

        if (cart is null)
        {
            return null;
        }

        var existingItem = cart.Items.SingleOrDefault(item =>
            item.CustomerCartItemId == request.CustomerCartItemId);
        if (existingItem is null)
        {
            return null;
        }

        if (request.Quantity > existingItem.Quantity)
        {
            var requestedCartQuantity = cart.Items
                .Where(item =>
                    item.ProductId == existingItem.ProductId &&
                    item.CustomerCartItemId != existingItem.CustomerCartItemId)
                .Sum(item => item.Quantity) + request.Quantity;
            await _inventoryStockService.EnsureCartQuantityAvailableAsync(
                existingItem.ProductId,
                request.FranchiseId,
                request.MartStoreId,
                requestedCartQuantity,
                cancellationToken);
        }

        var item = cart.UpdateItem(
            request.CustomerCartItemId,
            request.ProductNameSnapshot!,
            request.Quantity,
            request.UnitPrice,
            request.MRP,
            request.DiscountAmount,
            request.GSTPercent);

        if (item is null)
        {
            return null;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new CreatedCartItemDto(
            item.CustomerCartItemId,
            cart.CartNumber,
            item.ProductId,
            item.ProductNameSnapshot,
            item.Quantity,
            item.UnitPrice,
            item.MRP,
            item.DiscountAmount,
            item.GSTPercent,
            item.GSTAmount,
            item.LineTotal,
            item.AddedByCashierId,
            item.AddedOn);
    }
}
