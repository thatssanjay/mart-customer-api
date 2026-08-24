using Mart.Customer.Application.Orders.Dtos;
using MediatR;

namespace Mart.Customer.Application.Orders.Queries.GetOrderDetail;

public sealed record GetOrderDetailQuery(
    long CustomerOrderId,
    OrderDetailAccessScope AccessScope) : IRequest<OrderDetailResult>;
