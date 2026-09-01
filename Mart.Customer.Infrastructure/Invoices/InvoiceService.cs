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

    public async Task<InvoiceDownloadResult> GetOriginalInvoiceAsync(
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
        if (lookup.Status is InvoiceDocumentLookupStatus.OrderNotFound or InvoiceDocumentLookupStatus.Missing ||
            lookup.Document is null)
        {
            return InvoiceDownloadResult.NotFound();
        }

        try
        {
            var content = await _storage.OpenReadAsync(lookup.Document.StoragePath, cancellationToken);
            var fileName = $"{SanitizeFileName(lookup.InvoiceNumber ?? $"invoice-{customerOrderId}")}.pdf";
            return InvoiceDownloadResult.Found(content, fileName);
        }
        catch (FileNotFoundException)
        {
            return InvoiceDownloadResult.NotFound();
        }
        catch (DirectoryNotFoundException)
        {
            return InvoiceDownloadResult.NotFound();
        }
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
        const decimal pageWidth = 226.77m; // 80 mm thermal roll at 72 DPI.
        const decimal margin = 10m;
        const decimal contentWidth = pageWidth - (margin * 2m);

        var rows = BuildReceiptRows(invoice, margin, contentWidth);
        var contentHeight = rows.Sum(row => row.Height);
        var pageHeight = Math.Max(180m, contentHeight + (margin * 2m));
        var content = new StringBuilder();
        var top = pageHeight - margin;

        foreach (var row in rows)
        {
            if (row.DrawSeparator)
            {
                var separatorY = top - (row.Height / 2m);
                content.Append("q [2 2] 0 d 0.45 w ")
                    .Append(PdfNumber(margin)).Append(' ').Append(PdfNumber(separatorY)).Append(" m ")
                    .Append(PdfNumber(pageWidth - margin)).Append(' ').Append(PdfNumber(separatorY))
                    .Append(" l S Q\n");
            }

            foreach (var text in row.Texts)
            {
                var y = top - text.BaselineOffset;
                var x = text.Alignment switch
                {
                    ReceiptTextAlignment.Center => margin + ((contentWidth - EstimateTextWidth(text.Text, text.FontSize, text.FontName)) / 2m),
                    ReceiptTextAlignment.Right => margin + contentWidth - EstimateTextWidth(text.Text, text.FontSize, text.FontName),
                    _ => margin + text.LeftOffset
                };

                content.Append("BT /").Append(text.FontName).Append(' ')
                    .Append(PdfNumber(text.FontSize)).Append(" Tf ")
                    .Append(PdfNumber(Math.Max(margin, x))).Append(' ').Append(PdfNumber(y))
                    .Append(" Td (").Append(EscapePdfText(text.Text)).Append(") Tj ET\n");
            }

            top -= row.Height;
        }

        var objects = new[]
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {PdfNumber(pageWidth)} {PdfNumber(pageHeight)}] /Resources << /Font << /F1 5 0 R /F2 6 0 R /F3 7 0 R >> >> /Contents 4 0 R >>",
            $"<< /Length {Encoding.ASCII.GetByteCount(content.ToString())} >>\nstream\n{content}endstream",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Courier /Encoding /WinAnsiEncoding >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Courier-Bold /Encoding /WinAnsiEncoding >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>"
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

    private static List<ReceiptRow> BuildReceiptRows(
        OrderInvoiceSnapshotDto invoice,
        decimal margin,
        decimal contentWidth)
    {
        var rows = new List<ReceiptRow>
        {
            TextRow("SMART MART", 15m, "F3", ReceiptTextAlignment.Center, 20m),
            TextRow(invoice.StoreNameSnapshot, 8m, "F2", ReceiptTextAlignment.Center, 11m)
        };

        AddWrappedRows(rows, invoice.StoreAddressSnapshot, 50, 6.5m, ReceiptTextAlignment.Center);
        if (!string.IsNullOrWhiteSpace(invoice.StoreGSTINSnapshot))
        {
            rows.Add(TextRow($"GSTIN: {invoice.StoreGSTINSnapshot}", 6m, "F1", ReceiptTextAlignment.Center, 8m));
        }
        if (!string.IsNullOrWhiteSpace(invoice.StoreStateCodeSnapshot))
        {
            rows.Add(TextRow($"State code: {invoice.StoreStateCodeSnapshot}", 6m, "F1", ReceiptTextAlignment.Center, 8m));
        }
        rows.Add(SeparatorRow());
        rows.Add(KeyValueRow("Invoice", invoice.InvoiceNumber));
        rows.Add(KeyValueRow("Order", invoice.CustomerOrderId.ToString(CultureInfo.InvariantCulture)));
        rows.Add(KeyValueRow("Date", $"{invoice.OrderDate:dd-MMM-yyyy HH:mm} UTC"));

        var customerName = invoice.CustomerNameSnapshot
            ?? invoice.CustomerCodeSnapshot
            ?? invoice.CustomerId.ToString(CultureInfo.InvariantCulture);
        AddWrappedKeyValueRows(rows, "Customer", customerName, 38);
        if (!string.IsNullOrWhiteSpace(invoice.CustomerCodeSnapshot) &&
            !string.Equals(invoice.CustomerCodeSnapshot, customerName, StringComparison.Ordinal))
        {
            rows.Add(KeyValueRow("Code", invoice.CustomerCodeSnapshot));
        }
        if (!string.IsNullOrWhiteSpace(invoice.CustomerMobileSnapshot))
        {
            rows.Add(KeyValueRow("Mobile", invoice.CustomerMobileSnapshot));
        }
        if (!string.IsNullOrWhiteSpace(invoice.CustomerAddressSnapshot))
        {
            AddWrappedKeyValueRows(rows, "Address", invoice.CustomerAddressSnapshot, 38);
        }

        rows.Add(SeparatorRow());
        rows.Add(ProductHeaderRow(margin, contentWidth));
        rows.Add(SeparatorRow());

        foreach (var item in invoice.Items)
        {
            var productLines = WrapText(item.ProductName, 20).ToList();
            if (productLines.Count == 0)
            {
                productLines.Add("-");
            }

            rows.Add(ProductRow(
                productLines[0],
                FormatQuantity(item.Quantity),
                FormatMoney(item.UnitPrice),
                FormatMoney(item.GSTAmount),
                FormatMoney(item.DiscountAmount),
                FormatMoney(item.LineTotal),
                margin,
                contentWidth));

            foreach (var continuation in productLines.Skip(1))
            {
                rows.Add(TextRow(continuation, 5.2m, "F1", ReceiptTextAlignment.Left, 6.5m));
            }

            var itemDetail = $"MRP {FormatMoney(item.MRP)}  Gross {FormatMoney(item.GrossAmount)}  GST {FormatQuantity(item.GSTPercent)}%";
            rows.Add(TextRow(itemDetail, 5.2m, "F1", ReceiptTextAlignment.Left, 7m));
        }

        rows.Add(SeparatorRow());
        rows.Add(TotalRow("Subtotal", invoice.GrossAmount));
        rows.Add(TotalRow("Discount", invoice.DiscountAmount));
        if (invoice.CGSTAmount != 0m)
        {
            rows.Add(TotalRow("CGST", invoice.CGSTAmount));
        }
        if (invoice.SGSTAmount != 0m)
        {
            rows.Add(TotalRow("SGST", invoice.SGSTAmount));
        }
        if (invoice.IGSTAmount != 0m)
        {
            rows.Add(TotalRow("IGST", invoice.IGSTAmount));
        }
        rows.Add(TotalRow("Total Tax", invoice.GSTAmount));
        rows.Add(TotalRow("Net Amount", invoice.NetAmount));
        if (invoice.RoundOffAmount != 0m)
        {
            rows.Add(TotalRow("Round Off", invoice.RoundOffAmount));
        }
        if (invoice.RedeemPointsUsed != 0m)
        {
            rows.Add(TotalRow("Reward Points Redeemed", invoice.RedeemPointsUsed, monetary: false));
        }
        if (invoice.RedemptionAmount != 0m)
        {
            rows.Add(TotalRow("Reward Redemption", invoice.RedemptionAmount));
        }
        if (invoice.RewardEarned != 0m)
        {
            rows.Add(TotalRow("Reward Points Earned", invoice.RewardEarned, monetary: false));
        }
        if (invoice.CashbackEarned != 0m)
        {
            rows.Add(TotalRow("Cashback Earned", invoice.CashbackEarned));
        }
        rows.Add(SeparatorRow());
        rows.Add(TotalRow("GRAND TOTAL", invoice.FinalPayableAmount, emphasize: true));
        rows.Add(SeparatorRow());
        rows.Add(TextRow("PAYMENT DETAILS", 7m, "F2", ReceiptTextAlignment.Center, 10m));

        if (invoice.Payments.Count == 0)
        {
            rows.Add(TextRow("No payment details available", 6m, "F1", ReceiptTextAlignment.Center, 8m));
        }
        else
        {
            foreach (var payment in invoice.Payments)
            {
                rows.Add(KeyValueRow(payment.PaymentMode, $"INR {FormatMoney(payment.Amount)}", true));
                if (!string.IsNullOrWhiteSpace(payment.TransactionReference))
                {
                    AddWrappedKeyValueRows(rows, "Ref", payment.TransactionReference, 38);
                }
                rows.Add(KeyValueRow("Paid", $"{payment.PaidOn:dd-MMM-yyyy HH:mm} UTC"));
            }
        }

        rows.Add(SeparatorRow());
        rows.Add(TextRow("Thank you for shopping!", 8m, "F2", ReceiptTextAlignment.Center, 13m));
        if (!string.IsNullOrWhiteSpace(invoice.VerificationCode))
        {
            AddWrappedRows(rows, $"Verification: {invoice.VerificationCode}", 50, 5.5m, ReceiptTextAlignment.Center);
        }
        rows.Add(TextRow($"Invoice template: {invoice.InvoiceTemplateVersion}", 5.5m, "F1", ReceiptTextAlignment.Center, 8m));
        rows.Add(TextRow("*** SMART MART ***", 5.5m, "F1", ReceiptTextAlignment.Center, 8m));
        return rows;
    }

    private static ReceiptRow ProductHeaderRow(decimal margin, decimal contentWidth) =>
        ProductRow("ITEM", "QTY", "RATE", "TAX", "DISC", "TOTAL", margin, contentWidth, true);

    private static ReceiptRow ProductRow(
        string product,
        string quantity,
        string rate,
        string tax,
        string discount,
        string total,
        decimal margin,
        decimal contentWidth,
        bool isHeader = false)
    {
        var fontName = isHeader ? "F2" : "F1";
        var fontSize = isHeader ? 5.4m : 5.2m;
        var baseline = isHeader ? 6.2m : 5.8m;
        var nameWidth = 69m;
        var quantityWidth = 19m;
        var rateWidth = 30m;
        var taxWidth = 28m;
        var discountWidth = 28m;
        var totalWidth = contentWidth - nameWidth - quantityWidth - rateWidth - taxWidth - discountWidth;
        var texts = new List<ReceiptText>
        {
            new(product, fontName, fontSize, 0m, baseline, ReceiptTextAlignment.Left),
            RightAlignedCell(quantity, fontName, fontSize, nameWidth, quantityWidth, baseline),
            RightAlignedCell(rate, fontName, fontSize, nameWidth + quantityWidth, rateWidth, baseline),
            RightAlignedCell(tax, fontName, fontSize, nameWidth + quantityWidth + rateWidth, taxWidth, baseline),
            RightAlignedCell(discount, fontName, fontSize, nameWidth + quantityWidth + rateWidth + taxWidth, discountWidth, baseline),
            RightAlignedCell(total, fontName, fontSize, nameWidth + quantityWidth + rateWidth + taxWidth + discountWidth, totalWidth, baseline)
        };
        return new ReceiptRow(isHeader ? 8m : 7m, texts, false);
    }

    private static ReceiptText RightAlignedCell(
        string value,
        string fontName,
        decimal fontSize,
        decimal cellLeft,
        decimal cellWidth,
        decimal baseline) =>
        new(
            value,
            fontName,
            fontSize,
            cellLeft + cellWidth - EstimateTextWidth(value, fontSize, fontName),
            baseline,
            ReceiptTextAlignment.Left);

    private static ReceiptRow TotalRow(
        string label,
        decimal amount,
        bool emphasize = false,
        bool monetary = true)
    {
        var fontName = emphasize ? "F2" : "F1";
        var fontSize = emphasize ? 8m : 6.5m;
        var height = emphasize ? 11m : 8m;
        var baseline = emphasize ? 8m : 6.5m;
        return new ReceiptRow(
            height,
            [
                new ReceiptText(label, fontName, fontSize, 0m, baseline, ReceiptTextAlignment.Left),
                new ReceiptText(
                    monetary ? $"INR {FormatMoney(amount)}" : FormatQuantity(amount),
                    fontName,
                    fontSize,
                    0m,
                    baseline,
                    ReceiptTextAlignment.Right)
            ],
            false);
    }

    private static ReceiptRow KeyValueRow(string label, string value, bool emphasize = false)
    {
        var fontName = emphasize ? "F2" : "F1";
        var fontSize = emphasize ? 7m : 6.3m;
        var baseline = emphasize ? 7m : 6.3m;
        return new ReceiptRow(
            emphasize ? 9m : 8m,
            [
                new ReceiptText($"{label}:", fontName, fontSize, 0m, baseline, ReceiptTextAlignment.Left),
                new ReceiptText(value, fontName, fontSize, 0m, baseline, ReceiptTextAlignment.Right)
            ],
            false);
    }

    private static void AddWrappedKeyValueRows(
        ICollection<ReceiptRow> rows,
        string label,
        string value,
        int maxCharacters)
    {
        var wrapped = WrapText(value, maxCharacters).ToList();
        if (wrapped.Count == 0)
        {
            return;
        }

        rows.Add(KeyValueRow(label, wrapped[0]));
        foreach (var continuation in wrapped.Skip(1))
        {
            rows.Add(TextRow(continuation, 6.3m, "F1", ReceiptTextAlignment.Right, 8m));
        }
    }

    private static void AddWrappedRows(
        ICollection<ReceiptRow> rows,
        string value,
        int maxCharacters,
        decimal fontSize,
        ReceiptTextAlignment alignment)
    {
        foreach (var line in WrapText(value, maxCharacters))
        {
            rows.Add(TextRow(line, fontSize, "F1", alignment, fontSize + 2m));
        }
    }

    private static ReceiptRow TextRow(
        string text,
        decimal fontSize,
        string fontName,
        ReceiptTextAlignment alignment,
        decimal height) =>
        new(
            height,
            [new ReceiptText(text, fontName, fontSize, 0m, fontSize + 0.5m, alignment)],
            false);

    private static ReceiptRow SeparatorRow() => new(7m, [], true);

    private static IEnumerable<string> WrapText(string? value, int maxCharacters)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            yield break;
        }

        var remaining = string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        while (remaining.Length > maxCharacters)
        {
            var breakAt = remaining.LastIndexOf(' ', maxCharacters);
            if (breakAt <= 0)
            {
                breakAt = maxCharacters;
            }

            yield return remaining[..breakAt].Trim();
            remaining = remaining[breakAt..].TrimStart();
        }

        if (remaining.Length > 0)
        {
            yield return remaining;
        }
    }

    private static decimal EstimateTextWidth(string value, decimal fontSize, string fontName) =>
        value.Length * fontSize * (fontName == "F3" ? 0.56m : 0.6m);

    private static string FormatMoney(decimal value) => value.ToString("0.00", CultureInfo.InvariantCulture);

    private static string FormatQuantity(decimal value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    private static string PdfNumber(decimal value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    private static string EscapePdfText(string value) => new string(value
        .Select(character => character is >= ' ' and <= '~' ? character : '?')
        .ToArray())
        .Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("(", "\\(", StringComparison.Ordinal)
        .Replace(")", "\\)", StringComparison.Ordinal);

    private sealed record ReceiptRow(
        decimal Height,
        IReadOnlyList<ReceiptText> Texts,
        bool DrawSeparator);

    private sealed record ReceiptText(
        string Text,
        string FontName,
        decimal FontSize,
        decimal LeftOffset,
        decimal BaselineOffset,
        ReceiptTextAlignment Alignment);

    private enum ReceiptTextAlignment
    {
        Left,
        Center,
        Right
    }

    private static string SanitizeFileName(string value)
    {
        var safeValue = new string(value
            .Where(character => char.IsLetterOrDigit(character) || character is '-' or '_')
            .ToArray());
        return string.IsNullOrWhiteSpace(safeValue) ? "invoice" : safeValue;
    }
}
