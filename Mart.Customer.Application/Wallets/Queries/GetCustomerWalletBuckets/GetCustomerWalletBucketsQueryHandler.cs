using Mart.Customer.Application.Abstractions.Data;
using Mart.Customer.Application.Wallets.Dtos;
using MediatR;

namespace Mart.Customer.Application.Wallets.Queries.GetCustomerWalletBuckets;

public sealed class GetCustomerWalletBucketsQueryHandler
    : IRequestHandler<GetCustomerWalletBucketsQuery, IReadOnlyList<WalletBalanceBucketDto>?>
{
    private readonly ICustomerWalletRepository _customerWalletRepository;
    private readonly IWalletBalanceBucketRepository _walletBalanceBucketRepository;

    public GetCustomerWalletBucketsQueryHandler(
        ICustomerWalletRepository customerWalletRepository,
        IWalletBalanceBucketRepository walletBalanceBucketRepository)
    {
        _customerWalletRepository = customerWalletRepository;
        _walletBalanceBucketRepository = walletBalanceBucketRepository;
    }

    public async Task<IReadOnlyList<WalletBalanceBucketDto>?> Handle(
        GetCustomerWalletBucketsQuery request,
        CancellationToken cancellationToken)
    {
        var customerWalletId = await _customerWalletRepository.GetActiveIdAsync(
            request.CustomerId,
            request.WalletTypeId,
            cancellationToken,
            request.StoreId);

        if (!customerWalletId.HasValue)
        {
            return null;
        }

        return await _walletBalanceBucketRepository.GetByCustomerWalletIdAsync(
            customerWalletId.Value,
            request.AvailableOnly,
            request.IncludeSourceTransaction,
            cancellationToken);
    }
}
