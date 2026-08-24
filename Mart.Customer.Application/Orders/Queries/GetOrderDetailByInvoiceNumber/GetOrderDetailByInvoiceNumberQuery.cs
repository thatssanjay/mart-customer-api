using Mart.Customer.Application.Orders.Dtos;
using MediatR;

namespace Mart.Customer.Application.Orders.Queries.GetOrderDetailByInvoiceNumber;

public sealed record GetOrderDetailByInvoiceNumberQuery(
    string InvoiceNumber,
    OrderDetailAccessScope AccessScope) : IRequest<OrderDetailResult>;
