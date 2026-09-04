using Mart.Customer.Application.Wallets.Dtos;

namespace Mart.Customer.Application.Wallets.Services;

public interface IWalletRedemptionPreviewService
{
    Task<RedeemPreviewResultDto> PreviewAsync(
        long customerId,
        int walletTypeId,
        decimal amount,
        CancellationToken cancellationToken = default,
        long? storeId = null);
}
