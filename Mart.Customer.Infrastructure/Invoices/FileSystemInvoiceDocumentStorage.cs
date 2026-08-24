using Mart.Customer.Application.Orders.Services;
using Microsoft.Extensions.Options;

namespace Mart.Customer.Infrastructure.Invoices;

internal sealed class FileSystemInvoiceDocumentStorage : IInvoiceDocumentStorage
{
    private readonly string _archiveRoot;

    public FileSystemInvoiceDocumentStorage(IOptions<InvoiceArchiveOptions> options)
    {
        _archiveRoot = Path.GetFullPath(
            string.IsNullOrWhiteSpace(options.Value.ArchivePath)
                ? Path.Combine(AppContext.BaseDirectory, "invoice-archive")
                : options.Value.ArchivePath);
    }

    public async Task WriteAsync(
        string storagePath,
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken = default)
    {
        var absolutePath = ResolvePath(storagePath);
        Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);
        var temporaryPath = $"{absolutePath}.{Guid.NewGuid():N}.tmp";
        try
        {
            await File.WriteAllBytesAsync(temporaryPath, content.ToArray(), cancellationToken);
            File.Move(temporaryPath, absolutePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    public Task<Stream> OpenReadAsync(
        string storagePath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Stream stream = new FileStream(
            ResolvePath(storagePath),
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 64 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        return Task.FromResult(stream);
    }

    private string ResolvePath(string storagePath)
    {
        var absolutePath = Path.GetFullPath(Path.Combine(
            _archiveRoot,
            storagePath.Replace('/', Path.DirectorySeparatorChar)));
        var rootPrefix = _archiveRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!absolutePath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The invoice storage path is outside the configured archive root.");
        }

        return absolutePath;
    }
}
