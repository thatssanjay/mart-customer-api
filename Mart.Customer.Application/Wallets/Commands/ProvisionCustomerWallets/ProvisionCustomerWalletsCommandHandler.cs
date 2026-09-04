using Mart.Customer.Application.Abstractions.Data;
using Mart.Customer.Application.Wallets.Dtos;
using Mart.Customer.Domain.Common;
using Mart.Customer.Domain.Wallets;
using MediatR;

namespace Mart.Customer.Application.Wallets.Commands.ProvisionCustomerWallets;

public sealed class ProvisionCustomerWalletsCommandHandler
    : IRequestHandler<ProvisionCustomerWalletsCommand, ProvisionCustomerWalletsResultDto>
{
    private readonly ICustomerRepository _customerRepository;
    private readonly IWalletTypeRepository _walletTypeRepository;
    private readonly ICustomerWalletRepository _customerWalletRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ProvisionCustomerWalletsCommandHandler(
        ICustomerRepository customerRepository,
        IWalletTypeRepository walletTypeRepository,
        ICustomerWalletRepository customerWalletRepository,
        IUnitOfWork unitOfWork)
    {
        _customerRepository = customerRepository;
        _walletTypeRepository = walletTypeRepository;
        _customerWalletRepository = customerWalletRepository;
        _unitOfWork = unitOfWork;
    }

    public Task<ProvisionCustomerWalletsResultDto> Handle(
        ProvisionCustomerWalletsCommand request,
        CancellationToken cancellationToken) =>
        _unitOfWork.ExecuteInTransactionAsync(token => ProvisionAsync(request, token), cancellationToken);

    private async Task<ProvisionCustomerWalletsResultDto> ProvisionAsync(
        ProvisionCustomerWalletsCommand request,
        CancellationToken cancellationToken)
    {
        if (!await _customerRepository.ExistsByIdAsync(request.CustomerId, cancellationToken))
        {
            throw new DomainException("Customer not found.");
        }

        var activeWalletTypes = await _walletTypeRepository.GetAsync(
            activeOnly: true,
            cancellationToken);
        activeWalletTypes = activeWalletTypes
            .Where(type => !string.Equals(type.Code, WalletTypeCodes.MartWallet, StringComparison.OrdinalIgnoreCase))
            .ToList();
        var existingWallets = await _customerWalletRepository.GetByCustomerIdAsync(
            request.CustomerId,
            cancellationToken);
        var existingWalletsByTypeId = existingWallets.Where(wallet => wallet.StoreId == null).ToDictionary(wallet => wallet.WalletTypeId);
        var createdOn = DateTime.UtcNow;
        var createdWallets = activeWalletTypes
            .Where(walletType => !existingWalletsByTypeId.ContainsKey(walletType.Id))
            .Select(walletType => CustomerWallet.Create(request.CustomerId, walletType.Id, createdOn))
            .ToList();

        if (createdWallets.Count > 0)
        {
            await _customerWalletRepository.AddRangeAsync(createdWallets, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var created = activeWalletTypes
            .Join(
                createdWallets,
                walletType => walletType.Id,
                wallet => wallet.WalletTypeId,
                (walletType, wallet) => new CustomerWalletProvisioningItemDto(
                    wallet.CustomerWalletId,
                    wallet.WalletTypeId,
                    walletType.Code,
                    wallet.IsActive))
            .ToList();
        var skipped = activeWalletTypes
            .Where(walletType => existingWalletsByTypeId.ContainsKey(walletType.Id))
            .Select(walletType =>
            {
                var wallet = existingWalletsByTypeId[walletType.Id];
                return new CustomerWalletProvisioningItemDto(
                    wallet.CustomerWalletId,
                    wallet.WalletTypeId,
                    walletType.Code,
                    wallet.IsActive);
            })
            .ToList();

        return new ProvisionCustomerWalletsResultDto(request.CustomerId, created, skipped);
    }
}
