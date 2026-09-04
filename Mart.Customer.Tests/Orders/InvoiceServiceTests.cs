using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Mart.Customer.Application.Abstractions.Data;
using Mart.Customer.Application.Orders.Dtos;
using Mart.Customer.Application.Orders.Services;
using Mart.Customer.Domain.Orders;
using Mart.Customer.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Mart.Customer.Tests.Orders;

public sealed class InvoiceServiceTests
{
    [Fact]
    public async Task GetInvoice_WhenArchivedDocumentExists_StreamsItWithoutRegeneration()
    {
        var repository = new TestInvoiceRepository
        {
            Lookup = FoundDocument("invoices/archive.pdf")
        };
        var storage = new TestInvoiceStorage();
        storage.Files["invoices/archive.pdf"] = "%PDF-archived"u8.ToArray();
        await using var provider = CreateProvider(repository, storage);

        var result = await provider.GetRequiredService<IInvoiceService>().GetInvoiceAsync(
            101,
            CustomerAccess(7001));

        Assert.Equal(InvoiceDownloadStatus.Found, result.Status);
        Assert.Equal("INV-TEST-101.pdf", result.FileName);
        Assert.Equal("%PDF-archived", await ReadTextAsync(result.Content!));
        Assert.Equal(0, repository.SnapshotCalls);
        Assert.Equal(0, repository.InsertCalls);
        Assert.Equal(0, storage.WriteCalls);
        Assert.Equal(1, storage.OpenCalls);
    }

    [Fact]
    public async Task GetInvoice_WhenDocumentIsMissing_GeneratesStoresHashesAndIndexesPdf()
    {
        var repository = MissingRepository(Snapshot());
        var storage = new TestInvoiceStorage();
        await using var provider = CreateProvider(repository, storage);

        var result = await provider.GetRequiredService<IInvoiceService>().GetInvoiceAsync(
            101,
            CustomerAccess(7001));

        Assert.Equal(InvoiceDownloadStatus.Found, result.Status);
        Assert.NotNull(repository.InsertedDocument);
        var stored = Assert.Single(storage.Files);
        Assert.Equal(stored.Key, repository.InsertedDocument.StoragePath);
        Assert.Equal(stored.Value.LongLength, repository.InsertedDocument.FileSizeBytes);
        Assert.Equal(
            Convert.ToHexString(SHA256.HashData(stored.Value)),
            repository.InsertedDocument.Sha256Hash);
        Assert.StartsWith("%PDF-1.4", await ReadTextAsync(result.Content!));
    }

    [Fact]
    public async Task GetInvoice_WhenGenerating_UsesHistoricalTemplateAndOrderSnapshots()
    {
        var repository = MissingRepository(Snapshot(
            templateVersion: "historical-v7",
            productName: "Archived product"));
        var storage = new TestInvoiceStorage();
        await using var provider = CreateProvider(repository, storage);

        var result = await provider.GetRequiredService<IInvoiceService>().GetInvoiceAsync(
            101,
            CustomerAccess(7001));
        var pdfText = await ReadTextAsync(result.Content!);

        Assert.Contains("Invoice template: historical-v7", pdfText);
        Assert.Contains("Archived product", pdfText);
        Assert.Equal("historical-v7", repository.InsertedDocument!.InvoiceTemplateVersion);
    }

    [Fact]
    public async Task GetInvoice_WhenStorageWriteFails_DoesNotInsertDocument()
    {
        var repository = MissingRepository(Snapshot());
        var storage = new TestInvoiceStorage { WriteFailure = new IOException("storage unavailable") };
        await using var provider = CreateProvider(repository, storage);

        var exception = await Assert.ThrowsAsync<IOException>(() =>
            provider.GetRequiredService<IInvoiceService>().GetInvoiceAsync(
                101,
                CustomerAccess(7001)));

        Assert.Equal("storage unavailable", exception.Message);
        Assert.Equal(0, repository.InsertCalls);
        Assert.Empty(storage.Files);
    }

    [Fact]
    public async Task GetInvoice_WhenGenerationIsConcurrent_ReturnsOneIndexedDocumentToBothCallers()
    {
        var repository = MissingRepository(Snapshot());
        repository.AlwaysReturnMissing = true;
        var storage = new TestInvoiceStorage();
        await using var provider = CreateProvider(repository, storage);
        var service = provider.GetRequiredService<IInvoiceService>();

        var results = await Task.WhenAll(
            service.GetInvoiceAsync(101, CustomerAccess(7001)),
            service.GetInvoiceAsync(101, CustomerAccess(7001)));

        Assert.All(results, result => Assert.Equal(InvoiceDownloadStatus.Found, result.Status));
        Assert.Equal(2, repository.InsertCalls);
        Assert.Equal(1, repository.CreatedDocumentCount);
        Assert.Single(storage.Files);
        Assert.Equal(
            await ReadTextAsync(results[0].Content!),
            await ReadTextAsync(results[1].Content!));
    }

    private static ServiceProvider CreateProvider(
        TestInvoiceRepository repository,
        TestInvoiceStorage storage)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Invoices:ArchivePath"] = Path.Combine(Path.GetTempPath(), "unused-invoice-tests")
            })
            .Build();
        var services = new ServiceCollection();
        services.AddSingleton<ICustomerOrderInvoiceRepository>(repository);
        services.AddInfrastructure(configuration);
        services.RemoveAll<IInvoiceDocumentStorage>();
        services.AddSingleton<IInvoiceDocumentStorage>(storage);
        return services.BuildServiceProvider();
    }

    private static TestInvoiceRepository MissingRepository(OrderInvoiceSnapshotDto snapshot) =>
        new()
        {
            Lookup = new InvoiceDocumentLookupResult(
                InvoiceDocumentLookupStatus.Missing,
                InvoiceNumber: snapshot.InvoiceNumber),
            Snapshot = snapshot
        };

    private static InvoiceDocumentLookupResult FoundDocument(string storagePath) =>
        new(
            InvoiceDocumentLookupStatus.Found,
            new InvoiceDocumentDto(1, 101, "historical-v1", storagePath, new string('A', 64), 13),
            "INV-TEST-101");

    private static OrderDetailAccessScope CustomerAccess(long customerId) =>
        new(customerId, null, null);

    private static OrderInvoiceSnapshotDto Snapshot(
        string templateVersion = "historical-v1",
        string productName = "Snapshot product") =>
        new(
            101,
            "INV-TEST-101",
            7001,
            7,
            11,
            "CUS-7001",
            "Historical customer",
            "9000000000",
            "Historical customer address",
            "Historical store",
            "Historical store address",
            new DateTime(2026, 8, 23, 10, 30, 0, DateTimeKind.Utc),
            110m,
            10m,
            18m,
            118m,
            0m,
            118m,
            templateVersion,
            [new OrderCheckoutItemDto(1, 2, 3, productName, 1m, 100m, 110m, 110m, 10m, 18m, 18m, 118m)],
            [new OrderCheckoutPaymentDto(1, "Cash", 118m, null, new DateTime(2026, 8, 23, 10, 30, 0, DateTimeKind.Utc))]);

    private static async Task<string> ReadTextAsync(Stream stream)
    {
        await using (stream)
        using (var reader = new StreamReader(stream, Encoding.ASCII))
        {
            return await reader.ReadToEndAsync();
        }
    }
}

internal sealed class TestInvoiceRepository : ICustomerOrderInvoiceRepository
{
    private readonly object _sync = new();
    private InvoiceDocumentDto? _createdDocument;

    public InvoiceDocumentLookupResult Lookup { get; init; } =
        new(InvoiceDocumentLookupStatus.OrderNotFound);
    public OrderInvoiceSnapshotDto? Snapshot { get; init; }
    public bool AlwaysReturnMissing { get; set; }
    public int SnapshotCalls { get; private set; }
    public int InsertCalls { get; private set; }
    public int CreatedDocumentCount { get; private set; }
    public CustomerOrderInvoiceDocument? InsertedDocument { get; private set; }

    public Task<InvoiceDocumentLookupResult> GetAuthorizedDocumentAsync(
        long customerOrderId,
        OrderDetailAccessScope accessScope,
        CancellationToken cancellationToken = default)
    {
        if (!AlwaysReturnMissing && _createdDocument is not null)
        {
            return Task.FromResult(new InvoiceDocumentLookupResult(
                InvoiceDocumentLookupStatus.Found,
                _createdDocument,
                Snapshot?.InvoiceNumber ?? Lookup.InvoiceNumber));
        }

        return Task.FromResult(Lookup);
    }

    public Task<OrderInvoiceSnapshotDto?> GetSnapshotAsync(
        long customerOrderId,
        CancellationToken cancellationToken = default)
    {
        SnapshotCalls++;
        return Task.FromResult(Snapshot);
    }

    public Task<InvoiceDocumentInsertResult> TryAddDocumentAsync(
        CustomerOrderInvoiceDocument document,
        CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            InsertCalls++;
            InsertedDocument = document;
            if (_createdDocument is null)
            {
                CreatedDocumentCount++;
                _createdDocument = ToDto(document);
                return Task.FromResult(new InvoiceDocumentInsertResult(true, _createdDocument));
            }

            return Task.FromResult(new InvoiceDocumentInsertResult(false, _createdDocument));
        }
    }

    private static InvoiceDocumentDto ToDto(CustomerOrderInvoiceDocument document) =>
        new(
            document.CustomerOrderInvoiceDocumentId,
            document.CustomerOrderId,
            document.InvoiceTemplateVersion,
            document.StoragePath,
            document.Sha256Hash,
            document.FileSizeBytes);
}

internal sealed class TestInvoiceStorage : IInvoiceDocumentStorage
{
    public ConcurrentDictionary<string, byte[]> Files { get; } = new(StringComparer.Ordinal);
    public Exception? WriteFailure { get; init; }
    public int WriteCalls;
    public int OpenCalls;

    public Task WriteAsync(
        string storagePath,
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref WriteCalls);
        if (WriteFailure is not null)
        {
            throw WriteFailure;
        }

        Files[storagePath] = content.ToArray();
        return Task.CompletedTask;
    }

    public Task<Stream> OpenReadAsync(
        string storagePath,
        CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref OpenCalls);
        Stream stream = new MemoryStream(Files[storagePath], writable: false);
        return Task.FromResult(stream);
    }
}
