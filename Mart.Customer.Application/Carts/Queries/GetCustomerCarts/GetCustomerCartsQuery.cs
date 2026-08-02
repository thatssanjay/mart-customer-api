using Mart.Customer.Application.Carts.Dtos;
using MediatR;

namespace Mart.Customer.Application.Carts.Queries.GetCustomerCarts;

public sealed record GetCustomerCartsQuery(
    long CustomerId,
    string? CartStatus) : IRequest<IReadOnlyList<CartDetailsDto>>;
