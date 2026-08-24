using Mart.Customer.Application.Wallets.Dtos;
using MediatR;

namespace Mart.Customer.Application.Wallets.Commands.CreditWallet;

public sealed record CreditWalletCommand(
    long CustomerId,
    int WalletTypeId,
    decimal Amount,
    string? ReferenceType,
    long? ReferenceId,
    string? Remarks,
    DateTime? ExpiryDate,
    string CreatedBy) : IRequest<CreditWalletResultDto>;
