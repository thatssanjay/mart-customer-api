using System.Linq.Expressions;
using Mart.Customer.Application.Abstractions.Data;
using Mart.Customer.Domain.Orders;
using Mart.Customer.Application.Orders.Dtos;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace Mart.Customer.Persistence.Repositories;

internal sealed class CustomerOrderRepository : ICustomerOrderRepository
{
    private readonly ApplicationDbContext _dbContext;

    public CustomerOrderRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<bool> ExistsByCartIdAsync(
        long customerCartId,
        CancellationToken cancellationToken = default) =>
        _dbContext.CustomerOrders.AnyAsync(
            order => order.CustomerCartId == customerCartId,
            cancellationToken);

    public async Task AddAsync(CustomerOrder order, CancellationToken cancellationToken = default)
    {
        var store = await _dbContext.MartStores
            .AsNoTracking()
            .Where(item =>
                item.StoreId == order.MartStoreId &&
                item.FranchiseId == order.FranchiseId)
            .Select(item => new
            {
                item.StoreName,
                item.AddressLine1,
                item.AddressLine2,
                item.City,
                item.State,
                item.Country,
                item.PinCode
            })
            .SingleOrDefaultAsync(cancellationToken);

        var customer = await _dbContext.Customers
            .AsNoTracking()
            .Where(item => item.CustomerId == order.CustomerId)
            .Select(item => new
            {
                item.CustomerCode,
                item.DisplayName,
                item.MobileNumber,
                item.AddressLine1,
                item.AddressLine2,
                item.City,
                item.State,
                item.Country,
                item.PinCode
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (store is not null)
        {
            order.SetMasterSnapshots(
                store.StoreName,
                JoinAddress(store.AddressLine1, store.AddressLine2, store.City, store.State, store.Country, store.PinCode),
                customer?.CustomerCode,
                customer?.DisplayName,
                customer?.MobileNumber,
                customer is null
                    ? null
                    : JoinAddress(customer.AddressLine1, customer.AddressLine2, customer.City, customer.State, customer.Country, customer.PinCode));

            var isIntraState = string.IsNullOrWhiteSpace(store.State) ||
                string.IsNullOrWhiteSpace(customer?.State) ||
                string.Equals(store.State.Trim(), customer.State.Trim(), StringComparison.OrdinalIgnoreCase);
            order.ApplyTaxSplit(isIntraState);
        }
        else if (_dbContext.Database.IsSqlServer())
        {
            throw new InvalidOperationException("The order store could not be found in mart.Store.");
        }

        var productIds = order.Items.Select(item => item.ProductId).Distinct().ToList();
        var products = await _dbContext.Products
            .AsNoTracking()
            .Where(product => productIds.Contains(product.ProductId) && product.IsActive)
            .Select(product => new
            {
                product.ProductId,
                product.ProductCode,
                product.ProductName
            })
            .ToDictionaryAsync(product => product.ProductId, cancellationToken);

        if (_dbContext.Database.IsSqlServer() && products.Count != productIds.Count)
        {
            throw new InvalidOperationException("One or more order products could not be found in inventory.productmaster.");
        }

        foreach (var item in order.Items)
        {
            if (products.TryGetValue(item.ProductId, out var product))
            {
                item.ApplyProductSnapshot(product.ProductCode, product.ProductName);
            }
        }

        await _dbContext.CustomerOrders.AddAsync(order, cancellationToken);
    }

    public async Task<string> GetNextInvoiceNumberAsync(
        CancellationToken cancellationToken = default)
    {
        if (_dbContext.Database.IsRelational() &&
            _dbContext.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException(
                "Invoice numbers must be allocated inside the checkout transaction.");
        }

        var updatedRows = await _dbContext.InvoiceSerialCounters
            .Where(counter =>
                counter.CounterName == InvoiceSerialCounter.InvoiceCounterName &&
                counter.CurrentSerial < InvoiceSerialCounter.MaximumSerial)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(
                    counter => counter.CurrentSerial,
                    counter => counter.CurrentSerial + 1),
                cancellationToken);

        if (updatedRows != 1)
        {
            var counterExists = await _dbContext.InvoiceSerialCounters
                .AsNoTracking()
                .AnyAsync(
                    counter => counter.CounterName == InvoiceSerialCounter.InvoiceCounterName,
                    cancellationToken);

            throw new InvalidOperationException(counterExists
                ? "The eight-digit invoice number sequence has been exhausted."
                : "The invoice serial counter has not been initialized.");
        }

        var nextSerial = await _dbContext.InvoiceSerialCounters
            .AsNoTracking()
            .Where(counter => counter.CounterName == InvoiceSerialCounter.InvoiceCounterName)
            .Select(counter => counter.CurrentSerial)
            .SingleAsync(cancellationToken);

        return nextSerial.ToString("D8", CultureInfo.InvariantCulture);
    }

    private static string JoinAddress(params string?[] parts)
    {
        var address = string.Join(", ", parts.Where(value => !string.IsNullOrWhiteSpace(value)));
        return string.IsNullOrWhiteSpace(address) ? "Address unavailable" : address;
    }

    public async Task<(IReadOnlyList<OrderHistoryItemDto> Orders, int TotalCount)> GetPagedByCustomerIdAsync(
        long customerId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.CustomerOrders
            .AsNoTracking()
            .Where(order => order.CustomerId == customerId);

        var totalCount = await query.CountAsync(cancellationToken);
        var orders = await query
            .OrderByDescending(order => order.OrderDate)
            .ThenByDescending(order => order.CustomerOrderId)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(order => new OrderHistoryItemDto(
                order.CustomerOrderId,
                order.InvoiceNumber,
                order.OrderDate,
                order.Items.Count,
                order.GrossAmount,
                order.DiscountAmount,
                order.GSTAmount,
                order.TaxableAmount + order.GSTAmount + order.RoundOffAmount,
                order.RedemptionAmount,
                order.FinalPayableAmount,
                order.RewardEarned,
                order.CashbackEarned,
                order.OrderStatus,
                _dbContext.CustomerOrderInvoiceDocuments.Any(document =>
                    document.CustomerOrderId == order.CustomerOrderId) ? "Archived" : "Pending",
                _dbContext.CustomerOrderInvoiceDocuments.Any(document =>
                    document.CustomerOrderId == order.CustomerOrderId)))
            .ToListAsync(cancellationToken);

        return (orders, totalCount);
    }

    public async Task<IReadOnlyList<PendingPointsOrderDto>> GetPendingPointsAsync(
        CancellationToken cancellationToken = default) =>
        await _dbContext.CustomerOrders
            .AsNoTracking()
            .Where(order =>
                !order.IsPointsAwarded &&
                order.Payments.Any(payment => payment.PaymentMode == "Wallet"))
            .OrderByDescending(order => order.OrderDate)
            .ThenByDescending(order => order.CustomerOrderId)
            .Select(order => new PendingPointsOrderDto(
                order.CustomerOrderId,
                order.InvoiceNumber,
                order.OrderDate,
                order.Items.Count,
                order.FinalPayableAmount,
                order.OrderStatus))
            .ToListAsync(cancellationToken);

    public Task<OrderPointsAwardStateDto?> GetPointsAwardStateAsync(
        long customerOrderId,
        CancellationToken cancellationToken = default) =>
        _dbContext.CustomerOrders
            .AsNoTracking()
            .Where(order =>
                order.CustomerOrderId == customerOrderId &&
                order.Payments.Any(payment => payment.PaymentMode == "Wallet"))
            .Select(order => new OrderPointsAwardStateDto(
                order.CustomerOrderId,
                order.FranchiseId,
                order.MartStoreId,
                order.IsPointsAwarded,
                order.PointsAwardedDate,
                order.PointsAwardedBy))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<bool> TryMarkPointsAwardedAsync(
        long customerOrderId,
        long franchiseId,
        long storeId,
        CancellationToken cancellationToken = default)
    {
        var updatedRows = await _dbContext.CustomerOrders
            .Where(order =>
                order.CustomerOrderId == customerOrderId &&
                order.FranchiseId == franchiseId &&
                order.MartStoreId == storeId &&
                order.Payments.Any(payment => payment.PaymentMode == "Wallet") &&
                !order.IsPointsAwarded)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(order => order.IsPointsAwarded, true)
                .SetProperty(order => order.PointsAwardedDate, order => DateTime.Now)
                .SetProperty(order => order.PointsAwardedBy, "System"),
                cancellationToken);

        return updatedRows == 1;
    }

    public async Task<(IReadOnlyList<OrderSearchItemDto> Orders, int TotalCount)> SearchPagedAsync(
        OrderSearchCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.CustomerOrders
            .AsNoTracking()
            .Where(order =>
                order.FranchiseId == criteria.FranchiseId &&
                order.MartStoreId == criteria.MartStoreId);

        if (!string.IsNullOrWhiteSpace(criteria.CustomerName))
        {
            var customerName = criteria.CustomerName.Trim();
            query = query.Where(order =>
                order.CustomerNameSnapshot != null &&
                EF.Functions.Like(order.CustomerNameSnapshot, $"%{customerName}%"));
        }

        if (!string.IsNullOrWhiteSpace(criteria.MobileNumber))
        {
            var mobileNumber = criteria.MobileNumber.Trim();
            query = query.Where(order => order.CustomerMobileSnapshot == mobileNumber);
        }

        if (!string.IsNullOrWhiteSpace(criteria.InvoiceNumber))
        {
            var invoiceNumber = criteria.InvoiceNumber.Trim();
            query = query.Where(order =>
                EF.Functions.Like(order.InvoiceNumber, $"%{invoiceNumber}%"));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var orders = await query
            .OrderByDescending(order => order.OrderDate)
            .ThenByDescending(order => order.CustomerOrderId)
            .Skip((criteria.PageNumber - 1) * criteria.PageSize)
            .Take(criteria.PageSize)
            .Select(order => new OrderSearchItemDto(
                order.CustomerOrderId,
                order.InvoiceNumber,
                order.OrderDate,
                order.CustomerNameSnapshot,
                order.CustomerMobileSnapshot,
                order.FinalPayableAmount,
                order.OrderStatus))
            .ToListAsync(cancellationToken);

        return (orders, totalCount);
    }

    public async Task<OrderDetailResult> GetDetailAsync(
        long customerOrderId,
        OrderDetailAccessScope accessScope,
        CancellationToken cancellationToken = default)
    {
        var header = await GetHeaderQuery(
                order => order.CustomerOrderId == customerOrderId)
            .SingleOrDefaultAsync(cancellationToken);

        return await GetDetailAsync(header, accessScope, cancellationToken);
    }

    public async Task<OrderDetailResult> GetDetailByInvoiceNumberAsync(
        string invoiceNumber,
        OrderDetailAccessScope accessScope,
        CancellationToken cancellationToken = default)
    {
        var header = await GetHeaderQuery(
                order => order.InvoiceNumber == invoiceNumber)
            .SingleOrDefaultAsync(cancellationToken);

        return await GetDetailAsync(header, accessScope, cancellationToken);
    }

    public async Task<InvoiceDetailsResult> GetInvoiceDetailsAsync(
        long customerOrderId,
        OrderDetailAccessScope accessScope,
        CancellationToken cancellationToken = default)
    {
        var header = await _dbContext.CustomerOrders
            .AsNoTracking()
            .Where(order => order.CustomerOrderId == customerOrderId)
            .Select(order => new
            {
                order.CustomerOrderId,
                order.InvoiceNumber,
                order.OrderDate,
                order.OrderStatus,
                order.CustomerId,
                order.CustomerCodeSnapshot,
                order.CustomerNameSnapshot,
                order.CustomerMobileSnapshot,
                order.CustomerAddressSnapshot,
                order.FranchiseId,
                order.MartStoreId,
                order.StoreNameSnapshot,
                order.StoreAddressSnapshot,
                order.StoreGSTINSnapshot,
                order.StoreStateCodeSnapshot,
                order.GrossAmount,
                order.DiscountAmount,
                order.TaxableAmount,
                order.CGSTAmount,
                order.SGSTAmount,
                order.IGSTAmount,
                order.GSTAmount,
                order.RoundOffAmount,
                order.RedemptionWalletTypeId,
                order.RedeemPointsUsed,
                order.RedemptionAmount,
                order.FinalPayableAmount,
                order.RewardEarned,
                order.CashbackEarned
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (header is null)
        {
            return InvoiceDetailsResult.NotFound();
        }

        var canAccess = accessScope.CustomerId is > 0
            ? header.CustomerId == accessScope.CustomerId.Value
            : accessScope.FranchiseId is > 0 &&
              accessScope.MartStoreId is > 0 &&
              header.FranchiseId == accessScope.FranchiseId.Value &&
              header.MartStoreId == accessScope.MartStoreId.Value;
        if (!canAccess)
        {
            return InvoiceDetailsResult.Forbidden();
        }

        var items = await _dbContext.CustomerOrderItems
            .AsNoTracking()
            .Where(item => item.CustomerOrderId == customerOrderId)
            .OrderBy(item => item.CustomerOrderItemId)
            .Select(item => new InvoiceDetailsItemDto(
                item.CustomerOrderItemId,
                item.ProductId,
                item.ProductCodeSnapshot,
                item.HSNCodeSnapshot,
                item.ProductNameSnapshot,
                item.Quantity,
                item.UnitPrice,
                item.MRP,
                item.Quantity * item.UnitPrice,
                item.DiscountAmount,
                item.TaxableAmount,
                item.GSTPercent,
                item.CGSTAmount,
                item.SGSTAmount,
                item.IGSTAmount,
                item.GSTAmount,
                item.LineTotal))
            .ToListAsync(cancellationToken);

        var payments = await _dbContext.CustomerOrderPayments
            .AsNoTracking()
            .Where(payment => payment.CustomerOrderId == customerOrderId)
            .OrderBy(payment => payment.CustomerOrderPaymentId)
            .Select(payment => new InvoiceDetailsPaymentDto(
                payment.CustomerOrderPaymentId,
                payment.PaymentMode,
                payment.Amount,
                payment.TransactionReference,
                payment.PaidOn))
            .ToListAsync(cancellationToken);

        return InvoiceDetailsResult.Found(new InvoiceDetailsDto(
            header.CustomerOrderId,
            header.InvoiceNumber,
            header.OrderDate,
            header.OrderStatus,
            new InvoiceCustomerSnapshotDto(
                header.CustomerId,
                header.CustomerCodeSnapshot,
                header.CustomerNameSnapshot,
                header.CustomerMobileSnapshot,
                header.CustomerAddressSnapshot),
            new InvoiceStoreSnapshotDto(
                header.FranchiseId,
                header.MartStoreId,
                header.StoreNameSnapshot,
                header.StoreAddressSnapshot,
                header.StoreGSTINSnapshot,
                header.StoreStateCodeSnapshot),
            items,
            payments,
            new InvoiceTaxTotalsDto(
                header.GrossAmount,
                header.DiscountAmount,
                header.TaxableAmount,
                header.CGSTAmount,
                header.SGSTAmount,
                header.IGSTAmount,
                header.GSTAmount,
                header.RoundOffAmount,
                header.TaxableAmount + header.GSTAmount + header.RoundOffAmount,
                header.FinalPayableAmount),
            new InvoiceRewardRedemptionDto(
                header.RedemptionWalletTypeId,
                header.RedeemPointsUsed,
                header.RedemptionAmount,
                header.RewardEarned,
                header.CashbackEarned)));
    }

    public Task<OrderVerificationDto?> GetVerificationAsync(
        string verificationCode,
        CancellationToken cancellationToken = default) =>
        _dbContext.CustomerOrders
            .AsNoTracking()
            .Where(order => order.VerificationCode == verificationCode)
            .Select(order => new OrderVerificationDto(
                order.InvoiceNumber,
                order.OrderDate,
                order.MartStoreId,
                order.FinalPayableAmount,
                order.OrderStatus,
                true))
            .SingleOrDefaultAsync(cancellationToken);

    private IQueryable<OrderDetailHeader> GetHeaderQuery(
        Expression<Func<CustomerOrder, bool>> predicate) =>
        _dbContext.CustomerOrders
            .AsNoTracking()
            .Where(predicate)
            .Select(order => new
            {
                order.CustomerOrderId,
                order.CustomerCartId,
                order.InvoiceNumber,
                order.CustomerId,
                order.FranchiseId,
                order.MartStoreId,
                order.OrderDate,
                TotalItemCount = order.Items.Count,
                order.GrossAmount,
                order.DiscountAmount,
                order.GSTAmount,
                NetAmount = order.TaxableAmount + order.GSTAmount + order.RoundOffAmount,
                RedemptionWalletTypeId = (int?)null,
                order.RedemptionAmount,
                order.FinalPayableAmount,
                order.RewardEarned,
                order.CashbackEarned,
                order.OrderStatus,
                InvoiceStatus = _dbContext.CustomerOrderInvoiceDocuments.Any(document =>
                    document.CustomerOrderId == order.CustomerOrderId) ? "Archived" : "Pending",
                IsInvoiceDocumentAvailable =
                    _dbContext.CustomerOrderInvoiceDocuments.Any(document =>
                        document.CustomerOrderId == order.CustomerOrderId)
            })
            .Select(order => new OrderDetailHeader(
                order.CustomerOrderId,
                order.CustomerCartId,
                order.InvoiceNumber,
                order.CustomerId,
                order.FranchiseId,
                order.MartStoreId,
                order.OrderDate,
                order.TotalItemCount,
                order.GrossAmount,
                order.DiscountAmount,
                order.GSTAmount,
                order.NetAmount,
                order.RedemptionWalletTypeId,
                order.RedemptionAmount,
                order.FinalPayableAmount,
                order.RewardEarned,
                order.CashbackEarned,
                order.OrderStatus,
                order.InvoiceStatus,
                order.IsInvoiceDocumentAvailable));

    private async Task<OrderDetailResult> GetDetailAsync(
        OrderDetailHeader? header,
        OrderDetailAccessScope accessScope,
        CancellationToken cancellationToken)
    {
        if (header is null)
        {
            return OrderDetailResult.NotFound();
        }

        var canAccess = accessScope.CustomerId is > 0
            ? header.CustomerId == accessScope.CustomerId.Value
            : header.FranchiseId == accessScope.FranchiseId!.Value &&
              header.MartStoreId == accessScope.MartStoreId!.Value;

        if (!canAccess)
        {
            return OrderDetailResult.Forbidden();
        }

        var items = await _dbContext.CustomerOrderItems
            .AsNoTracking()
            .Where(item => item.CustomerOrderId == header.CustomerOrderId)
            .OrderBy(item => item.CustomerOrderItemId)
            .Select(item => new OrderDetailItemDto(
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
            .Where(payment => payment.CustomerOrderId == header.CustomerOrderId)
            .OrderBy(payment => payment.CustomerOrderPaymentId)
            .Select(payment => new OrderDetailPaymentDto(
                payment.CustomerOrderPaymentId,
                payment.PaymentMode,
                payment.Amount,
                payment.TransactionReference,
                payment.PaidOn))
            .ToListAsync(cancellationToken);

        return OrderDetailResult.Found(new OrderDetailDto(
            header.CustomerOrderId,
            header.CustomerCartId,
            header.InvoiceNumber,
            header.CustomerId,
            header.FranchiseId,
            header.MartStoreId,
            header.OrderDate,
            header.TotalItemCount,
            header.GrossAmount,
            header.DiscountAmount,
            header.GSTAmount,
            header.NetAmount,
            header.RedemptionWalletTypeId,
            header.RedemptionAmount,
            header.FinalPayableAmount,
            header.RewardEarned,
            header.CashbackEarned,
            header.OrderStatus,
            header.InvoiceStatus,
            header.IsInvoiceDocumentAvailable,
            items,
            payments));
    }

    private sealed record OrderDetailHeader(
        long CustomerOrderId,
        long CustomerCartId,
        string InvoiceNumber,
        long CustomerId,
        long FranchiseId,
        long MartStoreId,
        DateTime OrderDate,
        int TotalItemCount,
        decimal GrossAmount,
        decimal DiscountAmount,
        decimal GSTAmount,
        decimal NetAmount,
        int? RedemptionWalletTypeId,
        decimal RedemptionAmount,
        decimal FinalPayableAmount,
        decimal RewardEarned,
        decimal CashbackEarned,
        string OrderStatus,
        string InvoiceStatus,
        bool IsInvoiceDocumentAvailable);

    public async Task<CustomerOrder?> GetByCartIdAsync(
        long customerCartId,
        bool tracking,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.CustomerOrders
            .Include(order => order.Items)
            .Include(order => order.Payments)
            .AsSplitQuery();

        if (!tracking)
        {
            query = query.AsNoTracking();
        }

        var order = await query.SingleOrDefaultAsync(
            order => order.CustomerCartId == customerCartId,
            cancellationToken);
        if (order is null)
        {
            return null;
        }

        var redemptionWalletTypeId = await GetRedemptionWalletTypeIdAsync(
            order.CustomerOrderId,
            cancellationToken);

        var archivePath = await _dbContext.CustomerOrderInvoiceDocuments
            .AsNoTracking()
            .Where(document => document.CustomerOrderId == order.CustomerOrderId)
            .Select(document => document.StoragePath)
            .SingleOrDefaultAsync(cancellationToken);
        order.RestorePersistenceState(order.Items.Count, archivePath, redemptionWalletTypeId);
        return order;
    }

    public Task<int?> GetRedemptionWalletTypeIdAsync(
        long customerOrderId,
        CancellationToken cancellationToken = default) =>
        (from transaction in _dbContext.WalletTransactions.AsNoTracking()
         join wallet in _dbContext.CustomerWallets.AsNoTracking()
             on transaction.CustomerWalletId equals wallet.CustomerWalletId
         where transaction.ReferenceType == "ORDER" &&
               transaction.ReferenceId == customerOrderId &&
               transaction.TransactionType == "REDEMPTION"
         select (int?)wallet.WalletTypeId)
        .SingleOrDefaultAsync(cancellationToken);
}
