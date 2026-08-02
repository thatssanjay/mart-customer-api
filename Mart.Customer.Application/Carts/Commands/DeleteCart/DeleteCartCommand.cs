using MediatR;

namespace Mart.Customer.Application.Carts.Commands.DeleteCart;

public sealed record DeleteCartCommand(
    string CartNumber,
    long FranchiseId,
    long MartStoreId) : IRequest<bool>;
