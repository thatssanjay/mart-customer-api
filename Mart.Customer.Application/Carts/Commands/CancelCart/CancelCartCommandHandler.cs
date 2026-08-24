using Mart.Customer.Application.Abstractions.Data;
using Mart.Customer.Application.Carts.Dtos;
using Mart.Customer.Domain.Common;
using MediatR;

namespace Mart.Customer.Application.Carts.Commands.CancelCart;

public sealed class CancelCartCommandHandler : IRequestHandler<CancelCartCommand, CancelledCartDto?>
{
    private readonly ICustomerCartRepository _cartRepository;
    private readonly ICustomerOrderRepository _orderRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CancelCartCommandHandler(
        ICustomerCartRepository cartRepository,
        ICustomerOrderRepository orderRepository,
        IUnitOfWork unitOfWork)
    {
        _cartRepository = cartRepository;
        _orderRepository = orderRepository;
        _unitOfWork = unitOfWork;
    }

    public Task<CancelledCartDto?> Handle(
        CancelCartCommand request,
        CancellationToken cancellationToken) =>
        _unitOfWork.ExecuteInTransactionAsync(async transactionCancellationToken =>
        {
            var cart = await _cartRepository.GetByIdAsync(
                request.CustomerCartId,
                request.FranchiseId,
                request.MartStoreId,
                transactionCancellationToken);

            if (cart is null)
            {
                return null;
            }

            if (!string.Equals(cart.CartStatus, "Active", StringComparison.OrdinalIgnoreCase) ||
                cart.PaidOn.HasValue)
            {
                throw new DomainException("Only an active unpaid cart can be cancelled.");
            }

            if (await _orderRepository.ExistsByCartIdAsync(
                    cart.CustomerCartId,
                    transactionCancellationToken))
            {
                throw new DomainException("A cart with an existing order cannot be cancelled.");
            }

            var cancelledOn = DateTime.UtcNow;
            cart.Cancel(request.Remarks, cancelledOn);
            await _unitOfWork.SaveChangesAsync(transactionCancellationToken);

            return new CancelledCartDto(
                cart.CustomerCartId,
                cart.CartNumber,
                cart.CartStatus,
                cancelledOn,
                cart.Remarks);
        }, cancellationToken);
}
