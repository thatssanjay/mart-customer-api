using Mart.Customer.Application.Wallets.Dtos;
using MediatR;

namespace Mart.Customer.Application.Wallets.Queries.GetWalletTransactionByNumber;

public sealed record GetWalletTransactionByNumberQuery(string TransactionNumber)
    : IRequest<WalletTransactionDetailDto?>;
