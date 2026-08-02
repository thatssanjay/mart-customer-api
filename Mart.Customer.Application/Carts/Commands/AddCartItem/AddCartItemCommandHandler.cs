using Mart.Customer.Application.Abstractions.Data;
using Mart.Customer.Application.Carts.Dtos;
using MediatR;

namespace Mart.Customer.Application.Carts.Commands.AddCartItem;

public sealed class AddCartItemCommandHandler : IRequestHandler<AddCartItemCommand, CreatedCartItemDto?>
{
    private readonly ICustomerCartRepository _cartRepository;
    private readonly IUnitOfWork _unitOfWork;

    public AddCartItemCommandHandler(
        ICustomerCartRepository cartRepository,
        IUnitOfWork unitOfWork)
    {
        _cartRepository = cartRepository;
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
