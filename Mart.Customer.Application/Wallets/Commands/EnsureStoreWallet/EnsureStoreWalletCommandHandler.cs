using Mart.Customer.Application.Abstractions.Data;
using Mart.Customer.Domain.Common;
using Mart.Customer.Domain.Wallets;
using MediatR;

namespace Mart.Customer.Application.Wallets.Commands.EnsureStoreWallet;

public sealed class EnsureStoreWalletCommandHandler(
    IWalletTypeRepository walletTypes,
    ICustomerWalletRepository wallets,
    IUnitOfWork unitOfWork) : IRequestHandler<EnsureStoreWalletCommand>
{
    public async Task Handle(EnsureStoreWalletCommand request, CancellationToken cancellationToken)
    {
        if (request.CustomerId <= 0 || request.StoreId <= 0)
            throw new DomainException("A valid customer and store are required.");

        await unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            var activeTypes = await walletTypes.GetAsync(activeOnly: true, token);
            var martType = activeTypes.SingleOrDefault(type =>
                string.Equals(type.Code, WalletTypeCodes.MartWallet, StringComparison.OrdinalIgnoreCase));
            if (martType is null)
                return false;

            var existing = await wallets.GetByCustomerAndTypeAsync(
                request.CustomerId, martType.Id, token, request.StoreId);
            if (existing is not null)
                return false;

            await wallets.AddRangeAsync(
                [CustomerWallet.Create(request.CustomerId, martType.Id, DateTime.UtcNow, request.StoreId)],
                token);
            await unitOfWork.SaveChangesAsync(token);
            return true;
        }, cancellationToken);
    }
}
