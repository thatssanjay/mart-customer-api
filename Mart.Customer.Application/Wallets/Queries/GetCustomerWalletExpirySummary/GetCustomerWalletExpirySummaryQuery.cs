using Mart.Customer.Application.Wallets.Dtos;
using MediatR;

namespace Mart.Customer.Application.Wallets.Queries.GetCustomerWalletExpirySummary;

public sealed record GetCustomerWalletExpirySummaryQuery(
    long CustomerId,
    int WalletTypeId)
    : IRequest<WalletExpirySummaryDto?>;
