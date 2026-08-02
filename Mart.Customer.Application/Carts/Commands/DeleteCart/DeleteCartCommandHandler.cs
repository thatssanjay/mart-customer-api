using Mart.Customer.Application.Abstractions.Data;
using MediatR;

namespace Mart.Customer.Application.Carts.Commands.DeleteCart;

public sealed class DeleteCartCommandHandler : IRequestHandler<DeleteCartCommand, bool>
{
    private readonly ICustomerCartRepository _cartRepository;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteCartCommandHandler(
        ICustomerCartRepository cartRepository,
        IUnitOfWork unitOfWork)
    {
        _cartRepository = cartRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<bool> Handle(DeleteCartCommand request, CancellationToken cancellationToken)
    {
        var cart = await _cartRepository.GetByCartNumberAsync(
            request.CartNumber.Trim(),
            request.FranchiseId,
            request.MartStoreId,
            cancellationToken);

        if (cart is null)
        {
            return false;
        }

        _cartRepository.Remove(cart);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }
}
