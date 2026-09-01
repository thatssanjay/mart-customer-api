using Mart.Customer.Application.Orders.Dtos;
using MediatR;

namespace Mart.Customer.Application.Orders.Queries.GetInvoiceDetails;

public sealed record GetInvoiceDetailsQuery(
    long CustomerOrderId,
    OrderDetailAccessScope AccessScope) : IRequest<InvoiceDetailsResult>;
