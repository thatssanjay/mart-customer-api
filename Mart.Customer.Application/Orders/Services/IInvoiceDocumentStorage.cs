namespace Mart.Customer.Application.Orders.Services;

public interface IInvoiceDocumentStorage
{
    Task WriteAsync(
        string storagePath,
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken = default);

    Task<Stream> OpenReadAsync(
        string storagePath,
        CancellationToken cancellationToken = default);
}
