using Mart.Customer.Application.Abstractions.Data;
using Mart.Customer.Application.Wallets.Dtos;
using Mart.Customer.Application.Wallets.Services;
using MediatR;

namespace Mart.Customer.Application.Wallets.Queries.PreviewWalletRedemption;

public sealed class PreviewWalletRedemptionQueryHandler
    : IRequestHandler<PreviewWalletRedemptionQuery, RedeemPreviewResultDto>
{
    private readonly IWalletRedemptionPreviewService _previewService;

    public PreviewWalletRedemptionQueryHandler(
        ICustomerWalletRepository customerWalletRepository,
        IWalletBalanceBucketRepository walletBalanceBucketRepository)
    {
        _previewService = new WalletRedemptionPreviewService(
            customerWalletRepository,
            walletBalanceBucketRepository);
    }

    public async Task<RedeemPreviewResultDto> Handle(
        PreviewWalletRedemptionQuery request,
        CancellationToken cancellationToken)
    {
        return await _previewService.PreviewAsync(
            request.CustomerId,
            request.WalletTypeId,
            request.Amount,
            cancellationToken);
    }
}
