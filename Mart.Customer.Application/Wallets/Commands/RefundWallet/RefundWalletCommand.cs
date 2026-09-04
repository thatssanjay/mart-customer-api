using Mart.Customer.Application.Wallets.Dtos;
using MediatR;

namespace Mart.Customer.Application.Wallets.Commands.RefundWallet;

public sealed record RefundWalletCommand(
    long CustomerId,
    int WalletTypeId,
    string OriginalTransactionNumber,
    decimal Amount,
    string? Remarks,
    string CreatedBy,
    long? StoreId = null) : IRequest<RefundWalletResultDto>;
