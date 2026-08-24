namespace Mart.Customer.Infrastructure.Invoices;

public sealed class InvoiceArchiveOptions
{
    public const string SectionName = "Invoices";
    public string? ArchivePath { get; init; }
}
