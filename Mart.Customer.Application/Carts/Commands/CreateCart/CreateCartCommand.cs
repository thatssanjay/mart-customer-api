using Mart.Customer.Application.Carts.Dtos;
using MediatR;

namespace Mart.Customer.Application.Carts.Commands.CreateCart;

public sealed record CreateCartCommand(
    long CustomerId,
    long FranchiseId,
    long MartStoreId,
    long AddedByCashierId) : IRequest<CreatedCartDto>;
