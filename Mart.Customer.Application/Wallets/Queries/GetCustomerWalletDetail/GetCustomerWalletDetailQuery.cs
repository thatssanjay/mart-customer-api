using Mart.Customer.Application.Wallets.Dtos;
using MediatR;

namespace Mart.Customer.Application.Wallets.Queries.GetCustomerWalletDetail;

public sealed record GetCustomerWalletDetailQuery(long CustomerId, int WalletTypeId)
    : IRequest<CustomerWalletDto?>
{
    public long? StoreId { get; init; }
}
