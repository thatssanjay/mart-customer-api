using Mart.Customer.Application.Carts.Dtos;
using MediatR;

namespace Mart.Customer.Application.Carts.Commands.CancelCart;

public sealed record CancelCartCommand(
    long CustomerCartId,
    string? Remarks,
    long FranchiseId,
    long MartStoreId) : IRequest<CancelledCartDto?>;
