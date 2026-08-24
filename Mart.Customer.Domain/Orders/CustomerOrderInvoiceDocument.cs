namespace Mart.Customer.Domain.Orders;

public sealed class CustomerOrderInvoiceDocument
{
    private CustomerOrderInvoiceDocument() { }

    public long CustomerOrderInvoiceDocumentId { get; private set; }
    public long CustomerOrderId { get; private set; }
    public string DocumentType { get; private set; } = string.Empty;
    public string InvoiceTemplateVersion { get; private set; } = string.Empty;
    public string FileName { get; private set; } = string.Empty;
    public string MimeType { get; private set; } = "application/pdf";
    public string StoragePath { get; private set; } = string.Empty;
    public string Sha256Hash { get; private set; } = string.Empty;
    public long FileSizeBytes { get; private set; }
    public DateTime CreatedOn { get; private set; }

    public static CustomerOrderInvoiceDocument Create(
        long customerOrderId,
        string invoiceTemplateVersion,
        string storagePath,
        string sha256Hash,
        long fileSizeBytes,
        DateTime createdOn) =>
        new()
        {
            CustomerOrderId = customerOrderId,
            DocumentType = "Invoice",
            InvoiceTemplateVersion = invoiceTemplateVersion.Trim(),
            FileName = Path.GetFileName(storagePath.Trim()),
            StoragePath = storagePath.Trim(),
            Sha256Hash = sha256Hash.Trim(),
            FileSizeBytes = fileSizeBytes,
            CreatedOn = createdOn
        };
}
