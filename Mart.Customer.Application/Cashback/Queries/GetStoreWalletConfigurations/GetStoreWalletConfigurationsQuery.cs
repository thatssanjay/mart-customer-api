using Mart.Customer.Application.Cashback.Dtos;
using MediatR;

namespace Mart.Customer.Application.Cashback.Queries.GetStoreWalletConfigurations;

public sealed record GetStoreWalletConfigurationsQuery(
    int PageNumber = 1,
    int PageSize = 20) : IRequest<StoreWalletConfigurationsPageDto>;
