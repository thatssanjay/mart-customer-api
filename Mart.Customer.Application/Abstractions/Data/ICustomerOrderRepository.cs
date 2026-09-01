using Mart.Customer.Domain.Orders;
using Mart.Customer.Application.Orders.Dtos;

namespace Mart.Customer.Application.Abstractions.Data;

public interface ICustomerOrderRepository
{
    Task<bool> ExistsByCartIdAsync(
        long customerCartId,
        CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<OrderHistoryItemDto> Orders, int TotalCount)> GetPagedByCustomerIdAsync(
        long customerId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<OrderSearchItemDto> Orders, int TotalCount)> SearchPagedAsync(
        OrderSearchCriteria criteria,
        CancellationToken cancellationToken = default);

    Task<CustomerOrder?> GetByCartIdAsync(
        long customerCartId,
        bool tracking,
        CancellationToken cancellationToken = default);

    Task<int?> GetRedemptionWalletTypeIdAsync(
        long customerOrderId,
        CancellationToken cancellationToken = default);

    Task AddAsync(CustomerOrder order, CancellationToken cancellationToken = default);

    Task<OrderDetailResult> GetDetailAsync(
        long customerOrderId,
        OrderDetailAccessScope accessScope,
        CancellationToken cancellationToken = default);

    Task<OrderDetailResult> GetDetailByInvoiceNumberAsync(
        string invoiceNumber,
        OrderDetailAccessScope accessScope,
        CancellationToken cancellationToken = default);

    Task<InvoiceDetailsResult> GetInvoiceDetailsAsync(
        long customerOrderId,
        OrderDetailAccessScope accessScope,
        CancellationToken cancellationToken = default);

    Task<OrderVerificationDto?> GetVerificationAsync(
        string verificationCode,
        CancellationToken cancellationToken = default);
}
