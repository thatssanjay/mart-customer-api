using Mart.Customer.Application.Abstractions.Data;
using Mart.Customer.Application.Carts.Dtos;
using Mart.Customer.Application.Inventory.Services;
using MediatR;

namespace Mart.Customer.Application.Carts.Commands.AddCartItem;

public sealed class AddCartItemCommandHandler : IRequestHandler<AddCartItemCommand, CreatedCartItemDto?>
{
    private readonly ICustomerCartRepository _cartRepository;
    private readonly IInventoryStockService _inventoryStockService;
    private readonly IUnitOfWork _unitOfWork;

    public AddCartItemCommandHandler(
        ICustomerCartRepository cartRepository,
        IInventoryStockService inventoryStockService,
        IUnitOfWork unitOfWork)
    {
        _cartRepository = cartRepository;
        _inventoryStockService = inventoryStockService;
        _unitOfWork = unitOfWork;
    }

    public async Task<CreatedCartItemDto?> Handle(
        AddCartItemCommand request,
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

        var requestedCartQuantity = cart.Items
            .Where(item => item.ProductId == request.ProductId)
            .Sum(item => item.Quantity) + request.Quantity;
        await _inventoryStockService.EnsureCartQuantityAvailableAsync(
            request.ProductId,
            request.FranchiseId,
            request.MartStoreId,
            requestedCartQuantity,
            cancellationToken);

        var item = cart.AddItem(
            request.ProductId,
            request.ProductNameSnapshot!,
            request.Quantity,
            request.UnitPrice,
            request.MRP,
            request.DiscountAmount,
            request.GSTPercent,
            request.AddedByCashierId);

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
