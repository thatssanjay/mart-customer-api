using Mart.Customer.Application.Wallets.Dtos;
using MediatR;

namespace Mart.Customer.Application.Wallets.Queries.GetWalletTypes;

public sealed record GetWalletTypesQuery(bool ActiveOnly) : IRequest<IReadOnlyList<WalletTypeDto>>;
