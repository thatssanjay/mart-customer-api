namespace Mart.Customer.Application.Orders.Dtos;

public sealed record OrderInvoiceSnapshotDto(
    long CustomerOrderId,
    string InvoiceNumber,
    long CustomerId,
    long FranchiseId,
    long MartStoreId,
    string? CustomerCodeSnapshot,
    string? CustomerNameSnapshot,
    string? CustomerMobileSnapshot,
    string? CustomerAddressSnapshot,
    string StoreNameSnapshot,
    string StoreAddressSnapshot,
    DateTime OrderDate,
    decimal GrossAmount,
    decimal DiscountAmount,
    decimal GSTAmount,
    decimal NetAmount,
    decimal RedemptionAmount,
    decimal FinalPayableAmount,
    string InvoiceTemplateVersion,
    IReadOnlyList<OrderCheckoutItemDto> Items,
    IReadOnlyList<OrderCheckoutPaymentDto> Payments);

public sealed record InvoiceArchiveResultDto(string ArchivePath);

public sealed record InvoiceDocumentDto(
    long CustomerOrderInvoiceDocumentId,
    long CustomerOrderId,
    string InvoiceTemplateVersion,
    string StoragePath,
    string Sha256Hash,
    long FileSizeBytes);

public enum InvoiceDocumentLookupStatus
{
    Found,
    Missing,
    Forbidden,
    OrderNotFound
}

public sealed record InvoiceDocumentLookupResult(
    InvoiceDocumentLookupStatus Status,
    InvoiceDocumentDto? Document = null,
    string? InvoiceNumber = null);

public sealed record InvoiceDocumentInsertResult(
    bool Created,
    InvoiceDocumentDto Document);

public enum InvoiceDownloadStatus
{
    Found,
    Forbidden,
    NotFound
}

public sealed record InvoiceDownloadResult(
    InvoiceDownloadStatus Status,
    Stream? Content = null,
    string? FileName = null)
{
    public static InvoiceDownloadResult Found(Stream content, string fileName) =>
        new(InvoiceDownloadStatus.Found, content, fileName);

    public static InvoiceDownloadResult Forbidden() => new(InvoiceDownloadStatus.Forbidden);

    public static InvoiceDownloadResult NotFound() => new(InvoiceDownloadStatus.NotFound);
}
