using Mart.Customer.Application.Carts.Dtos;
using MediatR;

namespace Mart.Customer.Application.Carts.Commands.PayCartWithWallets;

public sealed record PayCartWithWalletsCommand(
    long CartId,
    string? CartNumber,
    string? PaymentToken,
    long CustomerId,
    string CreatedBy,
    IReadOnlyList<CartWalletDeduction> Deductions) : IRequest<CartWalletPaymentResultDto>;

public sealed record CartWalletDeduction(string? WalletCode, decimal Amount);
