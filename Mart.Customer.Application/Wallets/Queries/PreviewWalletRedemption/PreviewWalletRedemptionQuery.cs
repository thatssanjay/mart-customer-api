using Mart.Customer.Application.Wallets.Dtos;
using MediatR;

namespace Mart.Customer.Application.Wallets.Queries.PreviewWalletRedemption;

public sealed record PreviewWalletRedemptionQuery(
    long CustomerId,
    int WalletTypeId,
    decimal Amount) : IRequest<RedeemPreviewResultDto>;
