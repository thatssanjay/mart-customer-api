using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Mart.Customer.Application.Abstractions.Data;
using Mart.Customer.Application.Orders.Dtos;
using Mart.Customer.Application.Orders.Services;
using Mart.Customer.Domain.Orders;

namespace Mart.Customer.Infrastructure.Invoices;

internal sealed class InvoiceService : IInvoiceService
{
    private readonly ICustomerOrderInvoiceRepository _repository;
    private readonly IInvoiceDocumentStorage _storage;

    public InvoiceService(
        ICustomerOrderInvoiceRepository repository,
        IInvoiceDocumentStorage storage)
    {
        _repository = repository;
        _storage = storage;
    }

    public async Task<InvoiceDownloadResult> GetInvoiceAsync(
        long customerOrderId,
        OrderDetailAccessScope accessScope,
        CancellationToken cancellationToken = default)
    {
        var lookup = await _repository.GetAuthorizedDocumentAsync(
            customerOrderId,
            accessScope,
            cancellationToken);
        if (lookup.Status == InvoiceDocumentLookupStatus.Forbidden)
        {
            return InvoiceDownloadResult.Forbidden();
        }
        if (lookup.Status == InvoiceDocumentLookupStatus.OrderNotFound)
        {
            return InvoiceDownloadResult.NotFound();
        }

        var document = lookup.Document;
        if (document is null)
        {
            var snapshot = await _repository.GetSnapshotAsync(customerOrderId, cancellationToken);
            if (snapshot is null)
            {
                return InvoiceDownloadResult.NotFound();
            }

            var archive = await GenerateAndArchiveAsync(snapshot, cancellationToken);
            document = new InvoiceDocumentDto(
                0,
                snapshot.CustomerOrderId,
                snapshot.InvoiceTemplateVersion,
                archive.ArchivePath,
                string.Empty,
                0);
        }

        var content = await _storage.OpenReadAsync(document.StoragePath, cancellationToken);
        var fileName = $"{SanitizeFileName(lookup.InvoiceNumber ?? $"invoice-{customerOrderId}")}.pdf";
        return InvoiceDownloadResult.Found(content, fileName);
    }

    public async Task<InvoiceArchiveResultDto> GenerateAndArchiveAsync(
        OrderInvoiceSnapshotDto invoice,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(invoice);

        var storagePath = string.Join(
            '/',
            "invoices",
            invoice.OrderDate.ToString("yyyy", CultureInfo.InvariantCulture),
            invoice.OrderDate.ToString("MM", CultureInfo.InvariantCulture),
            invoice.CustomerOrderId.ToString(CultureInfo.InvariantCulture),
            $"{SanitizeFileName(invoice.InvoiceNumber)}.pdf");
        var pdf = BuildPdf(invoice);
        var sha256Hash = Convert.ToHexString(SHA256.HashData(pdf));

        await _storage.WriteAsync(storagePath, pdf, cancellationToken);

        var insert = await _repository.TryAddDocumentAsync(
            CustomerOrderInvoiceDocument.Create(
                invoice.CustomerOrderId,
                invoice.InvoiceTemplateVersion,
                storagePath,
                sha256Hash,
                pdf.LongLength,
                DateTime.UtcNow),
            cancellationToken);

        return new InvoiceArchiveResultDto(insert.Document.StoragePath);
    }

    private static byte[] BuildPdf(OrderInvoiceSnapshotDto invoice)
    {
        var lines = new List<string>
        {
            "MART INVOICE",
            $"Invoice: {invoice.InvoiceNumber}",
            $"Order: {invoice.CustomerOrderId}",
            $"Date: {invoice.OrderDate:yyyy-MM-dd HH:mm:ss} UTC",
            $"Store: {invoice.StoreNameSnapshot}",
            invoice.StoreAddressSnapshot,
            $"Customer: {invoice.CustomerNameSnapshot ?? invoice.CustomerCodeSnapshot ?? invoice.CustomerId.ToString(CultureInfo.InvariantCulture)}",
            invoice.CustomerAddressSnapshot ?? string.Empty,
            $"Template: {invoice.InvoiceTemplateVersion}",
            string.Empty
        };
        lines.AddRange(invoice.Items.Take(24).Select(item =>
            $"{item.ProductName}  {item.Quantity:0.###} x {item.UnitPrice:0.00}  {item.LineTotal:0.00}"));
        lines.AddRange([
            string.Empty,
            $"Gross: {invoice.GrossAmount:0.00}",
            $"Discount: {invoice.DiscountAmount:0.00}",
            $"GST: {invoice.GSTAmount:0.00}",
            $"Net: {invoice.NetAmount:0.00}",
            $"Wallet redemption: {invoice.RedemptionAmount:0.00}",
            $"Paid: {invoice.FinalPayableAmount:0.00}"
        ]);

        var content = new StringBuilder("BT\n/F1 10 Tf\n40 800 Td\n");
        foreach (var line in lines)
        {
            content.Append('(').Append(EscapePdfText(line)).Append(") Tj\n0 -16 Td\n");
        }
        content.Append("ET\n");

        var objects = new[]
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 5 0 R >> >> /Contents 4 0 R >>",
            $"<< /Length {Encoding.ASCII.GetByteCount(content.ToString())} >>\nstream\n{content}endstream",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>"
        };

        var pdf = new StringBuilder("%PDF-1.4\n");
        var offsets = new List<int> { 0 };
        for (var index = 0; index < objects.Length; index++)
        {
            offsets.Add(Encoding.ASCII.GetByteCount(pdf.ToString()));
            pdf.Append(index + 1).Append(" 0 obj\n").Append(objects[index]).Append("\nendobj\n");
        }

        var xrefOffset = Encoding.ASCII.GetByteCount(pdf.ToString());
        pdf.Append("xref\n0 ").Append(objects.Length + 1).Append("\n0000000000 65535 f \n");
        foreach (var offset in offsets.Skip(1))
        {
            pdf.Append(offset.ToString("D10", CultureInfo.InvariantCulture)).Append(" 00000 n \n");
        }
        pdf.Append("trailer\n<< /Size ").Append(objects.Length + 1)
            .Append(" /Root 1 0 R >>\nstartxref\n").Append(xrefOffset).Append("\n%%EOF");
        return Encoding.ASCII.GetBytes(pdf.ToString());
    }

    private static string EscapePdfText(string value) => value
        .Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("(", "\\(", StringComparison.Ordinal)
        .Replace(")", "\\)", StringComparison.Ordinal);

    private static string SanitizeFileName(string value)
    {
        var safeValue = new string(value
            .Where(character => char.IsLetterOrDigit(character) || character is '-' or '_')
            .ToArray());
        return string.IsNullOrWhiteSpace(safeValue) ? "invoice" : safeValue;
    }
}
