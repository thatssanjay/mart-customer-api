using Mart.Customer.Application.Wallets.Dtos;
using MediatR;

namespace Mart.Customer.Application.Wallets.Queries.GetCustomerWalletBuckets;

public sealed record GetCustomerWalletBucketsQuery(
    long CustomerId,
    int WalletTypeId,
    bool AvailableOnly = false,
    bool IncludeSourceTransaction = false)
    : IRequest<IReadOnlyList<WalletBalanceBucketDto>?>
{
    public long? StoreId { get; init; }
}
