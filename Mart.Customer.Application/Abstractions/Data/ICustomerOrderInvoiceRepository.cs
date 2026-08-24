using Mart.Customer.Application.Orders.Dtos;
using Mart.Customer.Domain.Orders;

namespace Mart.Customer.Application.Abstractions.Data;

public interface ICustomerOrderInvoiceRepository
{
    Task<InvoiceDocumentLookupResult> GetAuthorizedDocumentAsync(
        long customerOrderId,
        OrderDetailAccessScope accessScope,
        CancellationToken cancellationToken = default);

    Task<OrderInvoiceSnapshotDto?> GetSnapshotAsync(
        long customerOrderId,
        CancellationToken cancellationToken = default);

    Task<InvoiceDocumentInsertResult> TryAddDocumentAsync(
        CustomerOrderInvoiceDocument document,
        CancellationToken cancellationToken = default);
}
