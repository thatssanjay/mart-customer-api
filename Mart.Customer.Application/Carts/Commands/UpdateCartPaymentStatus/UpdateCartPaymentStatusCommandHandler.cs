using Mart.Customer.Application.Abstractions.Data;
using Mart.Customer.Application.Carts.Dtos;
using Mart.Customer.Domain.Common;
using MediatR;
using Mart.Customer.Application.Wallets.Commands.EnsureStoreWallet;

namespace Mart.Customer.Application.Carts.Commands.UpdateCartPaymentStatus;

public sealed class UpdateCartPaymentStatusCommandHandler(
    ICustomerCartRepository cartRepository,
    IUnitOfWork unitOfWork,
    ISender sender) : IRequestHandler<UpdateCartPaymentStatusCommand, UpdatedCartPaymentStatusDto>
{
    public Task<UpdatedCartPaymentStatusDto> Handle(
        UpdateCartPaymentStatusCommand request,
        CancellationToken cancellationToken) =>
        unitOfWork.ExecuteInTransactionAsync(token => UpdateAsync(request, token), cancellationToken);

    private async Task<UpdatedCartPaymentStatusDto> UpdateAsync(
        UpdateCartPaymentStatusCommand request,
        CancellationToken cancellationToken)
    {
        var cart = await cartRepository.GetByIdForWalletAsync(request.CartId, cancellationToken);
        if (cart is null || cart.CustomerId != request.CustomerId)
        {
            throw new DomainException("Invalid cart.");
        }

        var status = cart.UpdatePaymentStatus(request.Status!);
        if (status == "PAID")
        {
            await sender.Send(new EnsureStoreWalletCommand(cart.CustomerId, cart.MartStoreId), cancellationToken);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new UpdatedCartPaymentStatusDto(cart.CustomerCartId, status);
    }
}
