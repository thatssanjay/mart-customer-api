using Mart.Customer.Application.Abstractions.Data;
using Mart.Customer.Application.Wallets.Dtos;
using Mart.Customer.Domain.Common;

namespace Mart.Customer.Application.Wallets.Services;

public sealed class WalletRedemptionPreviewService : IWalletRedemptionPreviewService
{
    private readonly ICustomerWalletRepository _customerWalletRepository;
    private readonly IWalletBalanceBucketRepository _walletBalanceBucketRepository;

    public WalletRedemptionPreviewService(
        ICustomerWalletRepository customerWalletRepository,
        IWalletBalanceBucketRepository walletBalanceBucketRepository)
    {
        _customerWalletRepository = customerWalletRepository;
        _walletBalanceBucketRepository = walletBalanceBucketRepository;
    }

    public async Task<RedeemPreviewResultDto> PreviewAsync(
        long customerId,
        int walletTypeId,
        decimal amount,
        CancellationToken cancellationToken = default)
    {
        var wallet = await _customerWalletRepository.GetDetailIncludingInactiveAsync(
            customerId,
            walletTypeId,
            cancellationToken);
        if (wallet is null)
        {
            throw new DomainException("Customer wallet not found.");
        }

        if (!wallet.IsActive)
        {
            throw new DomainException("Customer wallet is inactive.");
        }

        if (!wallet.IsWalletTypeActive)
        {
            throw new DomainException("Wallet type is inactive.");
        }

        if (wallet.CurrentBalance < amount)
        {
            throw new DomainException("Insufficient wallet balance.");
        }

        var buckets = await _walletBalanceBucketRepository.GetByCustomerWalletIdAsync(
            wallet.CustomerWalletId,
            availableOnly: true,
            includeSourceTransaction: false,
            cancellationToken);
        var allocations = WalletRedemptionBucketCalculator.Calculate(
            buckets,
            amount,
            DateTime.UtcNow);

        return new RedeemPreviewResultDto(
            wallet.CustomerWalletId,
            wallet.CustomerId,
            wallet.WalletTypeId,
            amount,
            wallet.CurrentBalance,
            wallet.CurrentBalance - amount,
            allocations);
    }
}
