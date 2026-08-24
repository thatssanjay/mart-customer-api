using Mart.Customer.Application.Abstractions.Data;
using Mart.Customer.Application.Orders.Dtos;
using Mart.Customer.Domain.Orders;
using Microsoft.EntityFrameworkCore;

namespace Mart.Customer.Persistence.Repositories;

internal sealed class CustomerOrderInvoiceRepository : ICustomerOrderInvoiceRepository
{
    private readonly ApplicationDbContext _dbContext;

    public CustomerOrderInvoiceRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<InvoiceDocumentLookupResult> GetAuthorizedDocumentAsync(
        long customerOrderId,
        OrderDetailAccessScope accessScope,
        CancellationToken cancellationToken = default)
    {
        var owner = await _dbContext.CustomerOrders
            .AsNoTracking()
            .Where(order => order.CustomerOrderId == customerOrderId)
            .Select(order => new
            {
                order.CustomerId,
                order.FranchiseId,
                order.MartStoreId,
                order.InvoiceNumber,
                order.InvoiceTemplateVersion
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (owner is null)
        {
            return new InvoiceDocumentLookupResult(InvoiceDocumentLookupStatus.OrderNotFound);
        }

        var canAccess = accessScope.CustomerId is > 0
            ? owner.CustomerId == accessScope.CustomerId.Value
            : accessScope.FranchiseId is > 0 &&
              accessScope.MartStoreId is > 0 &&
              owner.FranchiseId == accessScope.FranchiseId.Value &&
              owner.MartStoreId == accessScope.MartStoreId.Value;
        if (!canAccess)
        {
            return new InvoiceDocumentLookupResult(InvoiceDocumentLookupStatus.Forbidden);
        }

        var document = await _dbContext.CustomerOrderInvoiceDocuments
            .AsNoTracking()
            .Where(item =>
                item.CustomerOrderId == customerOrderId)
            .Select(item => new InvoiceDocumentDto(
                item.CustomerOrderInvoiceDocumentId,
                item.CustomerOrderId,
                owner.InvoiceTemplateVersion,
                item.StoragePath,
                item.Sha256Hash,
                item.FileSizeBytes))
            .SingleOrDefaultAsync(cancellationToken);

        return document is null
            ? new InvoiceDocumentLookupResult(
                InvoiceDocumentLookupStatus.Missing,
                InvoiceNumber: owner.InvoiceNumber)
            : new InvoiceDocumentLookupResult(
                InvoiceDocumentLookupStatus.Found,
                document,
                owner.InvoiceNumber);
    }

    public async Task<OrderInvoiceSnapshotDto?> GetSnapshotAsync(
        long customerOrderId,
        CancellationToken cancellationToken = default)
    {
        var header = await _dbContext.CustomerOrders
            .AsNoTracking()
            .Where(order => order.CustomerOrderId == customerOrderId)
            .Select(order => new
            {
                order.CustomerOrderId,
                order.InvoiceNumber,
                order.CustomerId,
                order.FranchiseId,
                order.MartStoreId,
                order.CustomerCodeSnapshot,
                order.CustomerNameSnapshot,
                order.CustomerMobileSnapshot,
                order.CustomerAddressSnapshot,
                order.StoreNameSnapshot,
                order.StoreAddressSnapshot,
                order.OrderDate,
                order.GrossAmount,
                order.DiscountAmount,
                order.GSTAmount,
                NetAmount = order.TaxableAmount + order.GSTAmount + order.RoundOffAmount,
                order.RedemptionAmount,
                order.FinalPayableAmount,
                order.InvoiceTemplateVersion
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (header is null)
        {
            return null;
        }

        var items = await _dbContext.CustomerOrderItems
            .AsNoTracking()
            .Where(item => item.CustomerOrderId == customerOrderId)
            .OrderBy(item => item.CustomerOrderItemId)
            .Select(item => new OrderCheckoutItemDto(
                item.CustomerOrderItemId,
                0,
                item.ProductId,
                item.ProductNameSnapshot,
                item.Quantity,
                item.UnitPrice,
                item.MRP,
                item.Quantity * item.UnitPrice,
                item.DiscountAmount,
                item.GSTPercent,
                item.GSTAmount,
                item.LineTotal))
            .ToListAsync(cancellationToken);

        var payments = await _dbContext.CustomerOrderPayments
            .AsNoTracking()
            .Where(payment => payment.CustomerOrderId == customerOrderId)
            .OrderBy(payment => payment.CustomerOrderPaymentId)
            .Select(payment => new OrderCheckoutPaymentDto(
                payment.CustomerOrderPaymentId,
                payment.PaymentMode,
                payment.Amount,
                payment.TransactionReference,
                payment.PaidOn))
            .ToListAsync(cancellationToken);

        return new OrderInvoiceSnapshotDto(
            header.CustomerOrderId,
            header.InvoiceNumber,
            header.CustomerId,
            header.FranchiseId,
            header.MartStoreId,
            header.CustomerCodeSnapshot,
            header.CustomerNameSnapshot,
            header.CustomerMobileSnapshot,
            header.CustomerAddressSnapshot,
            header.StoreNameSnapshot,
            header.StoreAddressSnapshot,
            header.OrderDate,
            header.GrossAmount,
            header.DiscountAmount,
            header.GSTAmount,
            header.NetAmount,
            header.RedemptionAmount,
            header.FinalPayableAmount,
            header.InvoiceTemplateVersion,
            items,
            payments);
    }

    public async Task<InvoiceDocumentInsertResult> TryAddDocumentAsync(
        CustomerOrderInvoiceDocument document,
        CancellationToken cancellationToken = default)
    {
        _dbContext.CustomerOrderInvoiceDocuments.Add(document);
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            return new InvoiceDocumentInsertResult(true, ToDto(document));
        }
        catch (DbUpdateException)
        {
            _dbContext.Entry(document).State = EntityState.Detached;
            var existing = await _dbContext.CustomerOrderInvoiceDocuments
                .AsNoTracking()
                .Where(item =>
                    item.CustomerOrderId == document.CustomerOrderId)
                .Select(item => new InvoiceDocumentDto(
                    item.CustomerOrderInvoiceDocumentId,
                    item.CustomerOrderId,
                    "v1",
                    item.StoragePath,
                    item.Sha256Hash,
                    item.FileSizeBytes))
                .SingleOrDefaultAsync(cancellationToken);
            if (existing is null)
            {
                throw;
            }

            return new InvoiceDocumentInsertResult(false, existing);
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
