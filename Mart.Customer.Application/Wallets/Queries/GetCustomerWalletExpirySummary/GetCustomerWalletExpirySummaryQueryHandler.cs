using Mart.Customer.Application.Abstractions.Data;
using Mart.Customer.Application.Wallets.Dtos;
using MediatR;

namespace Mart.Customer.Application.Wallets.Queries.GetCustomerWalletExpirySummary;

public sealed class GetCustomerWalletExpirySummaryQueryHandler
    : IRequestHandler<GetCustomerWalletExpirySummaryQuery, WalletExpirySummaryDto?>
{
    private readonly IWalletBalanceBucketRepository _walletBalanceBucketRepository;

    public GetCustomerWalletExpirySummaryQueryHandler(
        IWalletBalanceBucketRepository walletBalanceBucketRepository)
    {
        _walletBalanceBucketRepository = walletBalanceBucketRepository;
    }

    public Task<WalletExpirySummaryDto?> Handle(
        GetCustomerWalletExpirySummaryQuery request,
        CancellationToken cancellationToken)
    {
        return _walletBalanceBucketRepository.GetExpirySummaryAsync(
            request.CustomerId,
            request.WalletTypeId,
            cancellationToken);
    }
}
