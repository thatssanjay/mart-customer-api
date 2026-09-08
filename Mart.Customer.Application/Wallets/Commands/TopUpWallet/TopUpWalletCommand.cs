using Mart.Customer.Application.Wallets.Dtos;
using MediatR;

namespace Mart.Customer.Application.Wallets.Commands.TopUpWallet;

public sealed record TopUpWalletCommand(
    long CustomerId,
    string WalletCode,
    decimal Amount,
    string PaymentMode,
    string? CardLast4,
    string? ReferenceNumber,
    string PaymentReference,
    string CreatedBy) : IRequest<WalletTopUpResultDto>;
