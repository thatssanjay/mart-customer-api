using Mart.Customer.Application.Orders.Dtos;

namespace Mart.Customer.Application.Orders.Services;

public interface IInvoiceService
{
    Task<InvoiceDownloadResult> GetInvoiceAsync(
        long customerOrderId,
        OrderDetailAccessScope accessScope,
        CancellationToken cancellationToken = default);

    Task<InvoiceDownloadResult> GetOriginalInvoiceAsync(
        long customerOrderId,
        OrderDetailAccessScope accessScope,
        CancellationToken cancellationToken = default);

    Task<InvoiceArchiveResultDto> GenerateAndArchiveAsync(
        OrderInvoiceSnapshotDto invoice,
        CancellationToken cancellationToken = default);
}
