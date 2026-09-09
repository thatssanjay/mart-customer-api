using Mart.Customer.Application.Wallets.Dtos;
using MediatR;

namespace Mart.Customer.Application.Wallets.Commands.ConvertRewardToStoreWallet;

public sealed record ConvertRewardToStoreWalletCommand(
    long CustomerId,
    long StoreId,
    decimal RewardPoints,
    string RequestId,
    string CreatedBy) : IRequest<RewardStoreWalletConversionResultDto>;
