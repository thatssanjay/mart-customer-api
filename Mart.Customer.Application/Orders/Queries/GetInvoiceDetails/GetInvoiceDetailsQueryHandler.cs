using Mart.Customer.Application.Abstractions.Data;
using Mart.Customer.Application.Orders.Dtos;
using MediatR;

namespace Mart.Customer.Application.Orders.Queries.GetInvoiceDetails;

public sealed class GetInvoiceDetailsQueryHandler
    : IRequestHandler<GetInvoiceDetailsQuery, InvoiceDetailsResult>
{
    private readonly ICustomerOrderRepository _orderRepository;

    public GetInvoiceDetailsQueryHandler(ICustomerOrderRepository orderRepository)
    {
        _orderRepository = orderRepository;
    }

    public Task<InvoiceDetailsResult> Handle(
        GetInvoiceDetailsQuery request,
        CancellationToken cancellationToken) =>
        _orderRepository.GetInvoiceDetailsAsync(
            request.CustomerOrderId,
            request.AccessScope,
            cancellationToken);
}
