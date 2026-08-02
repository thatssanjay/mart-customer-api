using MediatR;

namespace Mart.Customer.Application.Carts.Commands.DeleteCartItemsByProduct;

public sealed record DeleteCartItemsByProductCommand(
    string CartNumber,
    long ProductId,
    long FranchiseId,
    long MartStoreId) : IRequest<bool>;
