using Mart.Customer.Application.Abstractions.Data;
using Mart.Customer.Application.Orders.Dtos;
using MediatR;

namespace Mart.Customer.Application.Orders.Queries.GetOrderDetailByInvoiceNumber;

public sealed class GetOrderDetailByInvoiceNumberQueryHandler
    : IRequestHandler<GetOrderDetailByInvoiceNumberQuery, OrderDetailResult>
{
    private readonly ICustomerOrderRepository _orderRepository;

    public GetOrderDetailByInvoiceNumberQueryHandler(ICustomerOrderRepository orderRepository)
    {
        _orderRepository = orderRepository;
    }

    public Task<OrderDetailResult> Handle(
        GetOrderDetailByInvoiceNumberQuery request,
        CancellationToken cancellationToken) =>
        _orderRepository.GetDetailByInvoiceNumberAsync(
            request.InvoiceNumber.Trim(),
            request.AccessScope,
            cancellationToken);
}
