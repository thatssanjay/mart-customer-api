using Mart.Customer.Application.Abstractions.Data;
using Mart.Customer.Application.Wallets.Dtos;
using Mart.Customer.Domain.Common;
using MediatR;

namespace Mart.Customer.Application.Wallets.Commands.UpdateCustomerWalletStatus;

public sealed class UpdateCustomerWalletStatusCommandHandler
    : IRequestHandler<UpdateCustomerWalletStatusCommand, UpdatedCustomerWalletStatusDto?>
{
    private readonly ICustomerWalletRepository _customerWalletRepository;
    private readonly IWalletTypeRepository _walletTypeRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateCustomerWalletStatusCommandHandler(
        ICustomerWalletRepository customerWalletRepository,
        IWalletTypeRepository walletTypeRepository,
        IUnitOfWork unitOfWork)
    {
        _customerWalletRepository = customerWalletRepository;
        _walletTypeRepository = walletTypeRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<UpdatedCustomerWalletStatusDto?> Handle(
        UpdateCustomerWalletStatusCommand request,
        CancellationToken cancellationToken)
    {
        var wallet = await _customerWalletRepository.GetByCustomerAndTypeAsync(
            request.CustomerId,
            request.WalletTypeId,
            cancellationToken,
            request.StoreId);
        if (wallet is null)
        {
            return null;
        }

        var walletType = await _walletTypeRepository.GetByIdAsync(
            request.WalletTypeId,
            cancellationToken);
        if (walletType is null)
        {
            throw new DomainException("Wallet type not found.");
        }

        if (!walletType.IsActive)
        {
            throw new DomainException("Wallet type is inactive.");
        }

        wallet.UpdateStatus(request.IsActive!.Value, DateTime.UtcNow);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new UpdatedCustomerWalletStatusDto(
            wallet.CustomerWalletId,
            wallet.CustomerId,
            wallet.WalletTypeId,
            wallet.IsActive,
            wallet.ModifiedOn);
    }
}
