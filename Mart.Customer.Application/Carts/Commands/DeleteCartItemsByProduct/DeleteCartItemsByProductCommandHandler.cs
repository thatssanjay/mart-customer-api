using Mart.Customer.Application.Abstractions.Data;
using MediatR;

namespace Mart.Customer.Application.Carts.Commands.DeleteCartItemsByProduct;

public sealed class DeleteCartItemsByProductCommandHandler
    : IRequestHandler<DeleteCartItemsByProductCommand, bool>
{
    private readonly ICustomerCartRepository _cartRepository;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteCartItemsByProductCommandHandler(
        ICustomerCartRepository cartRepository,
        IUnitOfWork unitOfWork)
    {
        _cartRepository = cartRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<bool> Handle(
        DeleteCartItemsByProductCommand request,
        CancellationToken cancellationToken)
    {
        var cart = await _cartRepository.GetByCartNumberAsync(
            request.CartNumber.Trim(),
            request.FranchiseId,
            request.MartStoreId,
            cancellationToken);

        if (cart is null || !cart.RemoveItemsByProductId(request.ProductId))
        {
            return false;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}
