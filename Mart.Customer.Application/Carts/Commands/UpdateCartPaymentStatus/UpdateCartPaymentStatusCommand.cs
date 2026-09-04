using Mart.Customer.Application.Carts.Dtos;
using MediatR;

namespace Mart.Customer.Application.Carts.Commands.UpdateCartPaymentStatus;

public sealed record UpdateCartPaymentStatusCommand(
    long CartId,
    string? Status,
    long CustomerId) : IRequest<UpdatedCartPaymentStatusDto>;
