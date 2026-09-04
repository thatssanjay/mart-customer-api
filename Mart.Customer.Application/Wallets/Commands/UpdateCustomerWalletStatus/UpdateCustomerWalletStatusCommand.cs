using Mart.Customer.Application.Wallets.Dtos;
using MediatR;

namespace Mart.Customer.Application.Wallets.Commands.UpdateCustomerWalletStatus;

public sealed record UpdateCustomerWalletStatusCommand(
    long CustomerId,
    int WalletTypeId,
    bool? IsActive) : IRequest<UpdatedCustomerWalletStatusDto?>
{
    public long? StoreId { get; init; }
}
