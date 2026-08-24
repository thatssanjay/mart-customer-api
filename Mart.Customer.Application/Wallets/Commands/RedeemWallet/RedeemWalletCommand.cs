using Mart.Customer.Application.Wallets.Dtos;
using MediatR;

namespace Mart.Customer.Application.Wallets.Commands.RedeemWallet;

public sealed record RedeemWalletCommand(
    long CustomerId,
    int WalletTypeId,
    decimal Amount,
    string? ReferenceType,
    long? ReferenceId,
    string? Remarks,
    string CreatedBy) : IRequest<RedeemWalletResultDto>;
