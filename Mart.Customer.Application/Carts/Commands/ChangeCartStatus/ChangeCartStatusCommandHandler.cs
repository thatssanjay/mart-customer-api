using Mart.Customer.Application.Abstractions.Data;
using Mart.Customer.Application.Carts.Dtos;
using MediatR;

namespace Mart.Customer.Application.Carts.Commands.ChangeCartStatus;

public sealed class ChangeCartStatusCommandHandler : IRequestHandler<ChangeCartStatusCommand, ChangedCartStatusDto?>
{
    private readonly ICustomerCartRepository _cartRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ChangeCartStatusCommandHandler(
        ICustomerCartRepository cartRepository,
        IUnitOfWork unitOfWork)
    {
        _cartRepository = cartRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ChangedCartStatusDto?> Handle(
        ChangeCartStatusCommand request,
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

        cart.ChangeStatus(request.CartStatus!);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new ChangedCartStatusDto(
            cart.CartNumber,
            cart.CartStatus,
            cart.CustomerApprovedOn,
            cart.PaidOn,
            cart.CancelledOn,
            cart.ModifiedOn);
    }
}
